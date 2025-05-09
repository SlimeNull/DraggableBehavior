﻿using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace TestApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            var sampleData = Enumerable.Range(0, 28)
                .Select(v => v.ToString());

            SampleData = [.. sampleData];

            DataContext = this;
            InitializeComponent();
        }

        public ObservableCollection<string> SampleData { get; }
    }
}