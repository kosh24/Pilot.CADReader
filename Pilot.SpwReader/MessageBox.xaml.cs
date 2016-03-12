﻿using System.Windows;

namespace Ascon.Pilot.SDK.SpwReader
{
    /// <summary>
    /// Interaction logic for MessageBox.xaml
    /// </summary>
    public partial class MessageBox : Window
    {
        public MessageBox()
        {
            InitializeComponent();
            var strVersion = System.Reflection.Assembly.GetExecutingAssembly()
                .GetName().Version.ToString();
            VersionBlock.Text = "Версия: " + strVersion;

        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
