﻿using NLog;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Navigation;
using Ascon.Uln.KompasShell;
// ReSharper disable InconsistentNaming


namespace Ascon.Pilot.SDK.SpwReader
{
    [Export(typeof(IMainMenu))]
    [Export(typeof(IObjectContextMenu))]
    public class SpwReaderPlugin : IObjectContextMenu, IMainMenu
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private readonly IObjectModifier _objectModifier;
        private readonly IObjectsRepository _objectsRepository;
        private readonly IFileProvider _fileProvider;
        private readonly IEnumerable<IType> _pilotTypes;
        private readonly ObjectLoader _loader;
        private readonly List<IDataObject> _dataObjects;
        private const string ADD_INFORMATION_TO_PILOT = "ADD_INFORMATION_TO_PILOT";
        private const string ABOUT_PROGRAM_MENU = "ABOUT_PROGRAM_MENU";
        private const string SPW_EXT = ".spw";
        private const string SOURCE_DOC_EXT = ".cdw";
        // выбранный с помощью контекстного меню клиента объект
        private IDataObject _selected;
        // задача для открытия и анализа файла спецификации
        // список объктов спецификации полученных в ходе парсинга
        //private List<SpcObject> _listSpcObject;
        private KomapsShell _komaps;
        private bool _isKompasInit;

        [ImportingConstructor]
        public SpwReaderPlugin(IObjectModifier modifier, IObjectsRepository repository, IPersonalSettings personalSettings, IFileProvider fileProvider)
        {
            _objectModifier = modifier;
            _objectsRepository = repository;
            _fileProvider = fileProvider;
            _pilotTypes = _objectsRepository.GetTypes();
            _loader = new ObjectLoader(repository);
            _dataObjects = new List<IDataObject>();
        }

        public void BuildMenu(IMenuHost menuHost)
        {
            var menuItem = menuHost.GetItems().First();
            menuHost.AddSubItem(menuItem, ABOUT_PROGRAM_MENU, "О интеграции с КОМПАС", null, 0);
            menuHost.AddItem(ABOUT_PROGRAM_MENU, "О интеграции с КОМПАС", null, 1);
        }

        public void OnMenuItemClick(string itemName)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (itemName)
            {
                case ADD_INFORMATION_TO_PILOT:
                    SetInformationOnMenuClick(_selected);
                    break;
                case ABOUT_PROGRAM_MENU:
                    new AboutPluginBox().Show();
                    break;
            }
        }

        public void BuildContextMenu(IMenuHost menuHost, IEnumerable<IDataObject> selection, bool isContext)
        {
            if (isContext)
                return;

            var dataObjects = selection.ToArray();
            if (dataObjects.Count() != 1)
                return;

            var itemNames = menuHost.GetItems().ToList();
            const string indexItemName = "SetInfo";
            var insertIndex = itemNames.IndexOf(indexItemName) + 1;

            _selected = dataObjects.FirstOrDefault();
            if (_selected == null)
                return;
            if (!_selected.Type.IsMountable)
                return;

            var icon = IconLoader.GetIcon(@"/Resources/menu_icon.svg");
            menuHost.AddItem(ADD_INFORMATION_TO_PILOT, "Д_обавить информацию с диска", icon, insertIndex);
        }
        /// <summary>
        /// Очистка строки полученной из спецификации от служибных символов: $| и @/
        /// </summary>
        /// <param name="str">Строка которую нужно очистить</param>
        /// <returns>Очищенная строка</returns>
        private static string ValueTextClear(string str)
        {
            return str.Replace("$|", "").Replace(" @/", " ").Replace("@/", " ");
        }

        /// <summary>
        /// "Умный" поиск секции в спецификации, возвращает True если передана название секции спецификации по ГОСТ
        /// </summary>
        /// <param name="sectionName">Наименование секции в спецификации, например: Сборочные единицы</param>
        /// <param name="pattern">Шаблон для поиска секции</param>
        /// <returns>True or false</returns>
        private static bool ParsingSectionName(string sectionName, string pattern)
        {
            sectionName = sectionName.ToLower();
            return sectionName.Contains(pattern);
        }

        private static bool IsFileExtension(string name, string ext)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var theExt = Path.GetExtension(name).ToLower();
            return theExt == ext;
        }

        private void SetInformationOnMenuClick(IDataObject selected)
        {
            var listSpec = new List<Specification>();
            var storage = new StorageAnalayzer(_objectsRepository);
            var path = storage.GetProjectFolderByPilotStorage(selected);
            var filesSpw = storage.GetFilesSpw(path);
            foreach (var fileSpw in filesSpw)
            {
                var spc = GetInformationFromKompas(fileSpw);
                listSpec.Add(spc);
            }

            //var file = GetFileFromPilotStorage(selected, SPW_EXT);
            //if (file == null) return;
            //var info = GetInformationFromKompas(file);
            //if (info == null) return;
            //if (!info.Result.IsCompleted) return;
            //var kompasConverterTask = new Task(KompasConvert);
            //kompasConverterTask.Start();
            //kompasConverterTask.Wait();
            //IDataObject parent = null;
            //_loader.Load(selected.ParentId, o =>
            //{
            //    parent = o;
            //});
            //Thread.Sleep(100);
            //if (parent == null) return;
            //SynchronizeCheck(parent);
            //AddInformationToPilot(parent);

        }

        private void KompasConvert(List<SpcObject> listSpcObject)
        {
            _komaps = new KomapsShell();
            string message;
            _isKompasInit = _komaps.InitKompas(out message);
            if (!_isKompasInit) Logger.Error(message);
            foreach (var spcObject in listSpcObject)
            {
                var doc = spcObject.Documents.FirstOrDefault(f => IsFileExtension(f.FileName, SOURCE_DOC_EXT));
                if (doc == null) continue;
                var fileName = doc.FileName;
                if (!File.Exists(fileName)) continue;
                var pdfFile = Path.GetTempFileName() + ".pdf";
                var isConvert = _komaps.ConvertToPdf(fileName, pdfFile, out message);
                if (!isConvert)
                {
                    Logger.Error(message);
                    continue;
                }
                spcObject.PdfDocument = pdfFile;
            }
            _komaps.ExitKompas();
        }

        private IFile GetFileFromPilotStorage(IDataObject selected, string ext)
        {
            if (selected == null)
                return null;
            IFile file = null;
            _loader.Load(selected.RelatedSourceFiles.FirstOrDefault(), obj =>
            {
                file = obj.Files.FirstOrDefault(f => IsFileExtension(f.Name, ext));
            });
            return file;
        }

        private Specification GetInformationFromKompas(string filename)
        {
            var spc = new Specification { CurrentPath = filename };
            using (var inputStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                var ms = new MemoryStream();
                inputStream.Seek(0, SeekOrigin.Begin);
                inputStream.CopyTo(ms);
                ms.Position = 0;
                var taskOpenSpwFile = new Task<SpwAnalyzer>(() => new SpwAnalyzer(ms));
                taskOpenSpwFile.Start();
                taskOpenSpwFile.Wait();
                if (!taskOpenSpwFile.Result.IsCompleted)
                    return null;
                spc.ListSpcObjects = taskOpenSpwFile.Result.GetListSpcObject;
                return spc;
            }
        }

        //private Task<SpwAnalyzer> GetInformationFromKompas(IFile file)
        //{
        //    var inputStream = _fileProvider.OpenRead(file);
        //    if (inputStream == null)
        //        return null;
        //    if (!_fileProvider.Exists(file.Id))
        //        return null;
        //    var ms = new MemoryStream();
        //    inputStream.Seek(0, SeekOrigin.Begin);
        //    inputStream.CopyTo(ms);
        //    ms.Position = 0;

        //    _taskOpenSpwFile = new Task<SpwAnalyzer>(() => new SpwAnalyzer(ms));
        //    _taskOpenSpwFile.Start();
        //    _taskOpenSpwFile.Wait();
        //    if (!_taskOpenSpwFile.Result.IsCompleted)
        //        return null;
        //    _listSpcObject = _taskOpenSpwFile.Result.GetListSpcObject;
        //    return _taskOpenSpwFile;
        //}

        private void SynchronizeCheck(IDataObject parent, List<SpcObject> listSpcObject)
        {
            var children = parent.TypesByChildren;
            var loader = new LoaderOfObjects(_objectsRepository);
            _dataObjects.Clear();
            loader.Load(children.Keys, objects =>
            {
                foreach (var obj in objects)
                {
                    if (obj.Id == _selected.Id)
                        continue;
                    var attrNameValue = string.Empty;
                    var attrMarkValue = string.Empty;
                    foreach (var a in obj.Attributes)
                    {
                        if (a.Key == "name")
                            attrNameValue = a.Value.ToString();
                        if (a.Key == "mark")
                            attrMarkValue = a.Value.ToString();
                    }
                    foreach (var spcObj in listSpcObject)
                    {
                        bool isName = false, isMark = false;
                        foreach (var column in spcObj.Columns)
                        {
                            var colunmValue = ValueTextClear(column.Value);
                            if ((column.TypeName == "name") && (colunmValue == attrNameValue))
                                isName = true;
                            // TODO: здесь может быть проблема с объектами без обозначения и с дублирующими объектами необходимо тестирование и исследование
                            if ((column.TypeName == "mark") && (colunmValue == attrMarkValue) || attrMarkValue == string.Empty)
                                isMark = true;
                        }
                        if (isName && isMark)
                        {
                            spcObj.IsSynchronized = true;
                            spcObj.GlobalId = obj.Id;
                            _dataObjects.Add(obj);
                        }
                    }
                }
            });
        }

        private void UpdatePilotObject(SpcObject spcObject)
        {
            if (_dataObjects.Count == 0)
                return;
            var needToChange = false;
            var obj = _dataObjects.FirstOrDefault(o => spcObject.GlobalId == o.Id);
            if (obj == null)
                return;
            var builder = _objectModifier.Edit(obj);
            foreach (var spcColumn in spcObject.Columns)
            {
                var spcColVal = ValueTextClear(spcColumn.Value);
                // проверка нужно ли изменять объект
                foreach (var attrObj in obj.Attributes)
                {
                    if (attrObj.Key != spcColumn.TypeName) continue;
                    if (attrObj.Value.ToString() != spcColVal)
                    {
                        needToChange = true;
                        int i;
                        if (int.TryParse(spcColVal, out i))
                            builder.SetAttribute(spcColumn.TypeName, i);
                        else
                            builder.SetAttribute(spcColumn.TypeName, spcColVal);
                    }
                }
            }
            // получаем pdf файл из Обозревателя
            var fileFromPilot = obj.Files.FirstOrDefault(f => IsFileExtension(f.Name, ".pdf"));
            var doc = spcObject.Documents.FirstOrDefault(f => IsFileExtension(f.FileName, SOURCE_DOC_EXT));
            if (doc != null && fileFromPilot != null)
            {
                var pdfFile = spcObject.PdfDocument;
                // md5 в нижнем регистре расчитывается и возвращается пилотом
                var fileNameMd5 = CalculatorMd5Checksum.Go(pdfFile);
                if (!string.IsNullOrEmpty(fileNameMd5) && fileFromPilot.Md5 != fileNameMd5)
                {
                    needToChange = true;
                    builder.AddFile(pdfFile);
                }
            }
            //TODO: внесмотря на проекрку выдаётся ошибка, если изменился только чертёж
            if (needToChange) _objectModifier.Apply();
        }

        private void CreateNewObjectsToPilot(IDataObject parent, SpcObject spcObject)
        {
            var t = GetTypeBySectionName(spcObject.SectionName);
            if (t == null) return;
            var builder = _objectModifier.Create(parent, t);
            spcObject.GlobalId = builder.DataObject.Id;
            foreach (var attr in spcObject.Columns)
            {
                var val = attr.Value;
                if (string.IsNullOrEmpty(attr.TypeName) || string.IsNullOrEmpty(val)) continue;
                // очишаем значение от служебных символов и выражений
                val = ValueTextClear(val);
                // в качестве наименование передаётся внутренее имя (а не то которое отображается)
                int i;
                if (int.TryParse(val, out i))
                    builder.SetAttribute(attr.TypeName, i);
                else
                    builder.SetAttribute(attr.TypeName, val);
            }
            var doc = spcObject.Documents.FirstOrDefault(f => IsFileExtension(f.FileName, SOURCE_DOC_EXT));
            if (doc != null)
            {
                var fileName = doc.FileName;
                string[] paths = { fileName };
                var storageObjects = _objectsRepository.GetStorageObjects(paths);
                var storageObject = storageObjects.FirstOrDefault();
                if (storageObject != null)
                    builder.AddSourceFileRelation(storageObject.DataObject.Id);
                if (File.Exists(spcObject.PdfDocument))
                {
                    builder.AddFile(spcObject.PdfDocument);
                };

            }
            _objectModifier.Apply();
        }

        private void AddInformationToPilot(IDataObject parent, List<SpcObject> _listSpcObject)
        {
            foreach (var spcObject in _listSpcObject)
            {
                if (string.IsNullOrEmpty(spcObject.SectionName)) continue;
                if (!spcObject.IsSynchronized)
                {
                    CreateNewObjectsToPilot(parent, spcObject);
                }
                else
                {
                    UpdatePilotObject(spcObject);
                }
            }
        }

        private IType GetTypeBySectionName(string sectionName)
        {
            // ReSharper disable once RedundantAssignment
            var title = string.Empty;
            foreach (var itype in _pilotTypes)
            {
                title = itype.Title;
                if (ParsingSectionName(sectionName, "документ") && title == "Документ")
                    return itype;
                if (ParsingSectionName(sectionName, "комплекс") && title == "Комплекс")
                    return itype;
                if (ParsingSectionName(sectionName, "сборочн") && title == "Сборочная единица")
                    return itype;
                if (ParsingSectionName(sectionName, "детал") && title == "Деталь")
                    return itype;
                if (ParsingSectionName(sectionName, "стандарт") && title == "Стандартное изделие")
                    return itype;
                if (ParsingSectionName(sectionName, "проч") && title == "Прочее изделие")
                    return itype;
                if (ParsingSectionName(sectionName, "материал") && title == "Материал")
                    return itype;
                if (ParsingSectionName(sectionName, "комплект") && title == "Комплект")
                    return itype;
            }
            return null;
        }
    }
}
