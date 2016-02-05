﻿using Ascon.Pilot.SDK;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;

namespace Pilot.CADReader
{
    [Export(typeof(IObjectContextMenu))]
    public class CADReaderPlugin : IObjectContextMenu
    {
        private const string MenuItemName = "PluginMenuItem";
        private const string DocumentMenuItemName = "DocumentPluginMenuItem";
        private const string ContextMenuItemName = "ContextPluginMenuItem";
        private const string ManySelectedMenuItemName = "ManySelectedMenuItemName";

        public void BuildContextMenu(IMenuHost menuHost, IEnumerable<IDataObject> selection, bool isContext)
        {
            var objects = selection.ToList();
            var icon = IconLoader.GetIcon("/Resources/menu_icon.svg");

            var itemNames = menuHost.GetItems().ToList();
            const string indexItemName = "miShowSharingSettings";
            var insertIndex = itemNames.IndexOf(indexItemName) + 1;

            // Show menu for the empty area within a folder
            if (isContext)
            {
                menuHost.AddItem(ContextMenuItemName, "Menu for empty area", icon, insertIndex);
                return;
            }

            // Show menu when a document is selected
            if (objects.Count() == 1)
            {
                var current = objects.Single();
                if (current.Type.HasFiles)
                    menuHost.AddItem(DocumentMenuItemName, "Menu for document", icon, insertIndex);
                else
                    menuHost.AddItem(MenuItemName, "Menu for object", icon, insertIndex);
            }

            // Show menu for many selected objects
            if (objects.Count > 1)
            {
                menuHost.AddItem(ManySelectedMenuItemName, "Menu for many objects", icon, insertIndex);
            }
        }

        public void OnMenuItemClick(string itemName)
        {
            if (MenuItemName == itemName)
                MessageBox.Show(string.Format("{0} was clicked", MenuItemName));

            if (DocumentMenuItemName == itemName)
                MessageBox.Show(string.Format("{0} was clicked", DocumentMenuItemName));

            if (ContextMenuItemName == itemName)
                MessageBox.Show(string.Format("{0} was clicked", ContextMenuItemName));

            if (ManySelectedMenuItemName == itemName)
                MessageBox.Show(string.Format("{0} was clicked", ManySelectedMenuItemName));
        }
    }
}
