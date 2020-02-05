using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Xml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace UWPResourceViewer
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
            LoadData();
            
        }

        private void LoadData()
        {
            var versions = new DirectoryInfo("Resources").GetDirectories().Select(d=>d.Name).OrderByDescending(d=>d).ToArray();
            BuildNumbers.ItemsSource = versions;
            BuildNumbers.SelectedIndex = 0;
            //Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Resources/").LocalPath;
            //LoadResources("18362");
        }

        private void BuildNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadResources(BuildNumbers.SelectedValue as string);
        }

        private async void LoadResources(string version)
        {
            resources = null;
            var themeResourcesXaml = File.ReadAllText((await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Resources/{version}/themeresources.xaml"))).Path);
            var genericXaml = File.ReadAllText((await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Resources/{version}/generic.xaml"))).Path);
            var themeresources = Windows.UI.Xaml.Markup.XamlReader.Load(themeResourcesXaml) as ResourceDictionary;
            var generic = Windows.UI.Xaml.Markup.XamlReader.Load(genericXaml) as ResourceDictionary;
            var themes = themeresources.ThemeDictionaries.ToArray();
            var dics = themeresources.MergedDictionaries.ToArray();
            var byType = themeresources.GroupBy(g => g.Value.GetType()).ToArray();
            List<DataModel> items = new List<DataModel>();
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(themeResourcesXaml);
            System.Xml.XmlNamespaceManager manager = new System.Xml.XmlNamespaceManager(new NameTable());
            manager.AddNamespace("d", "http://schemas.microsoft.com/winfx/2006/xaml/presentation");
            manager.AddNamespace("x", "http://schemas.microsoft.com/winfx/2006/xaml");
            var root = doc.DocumentElement;
            List<ResourceDictionaryModel> themeModels = new List<ResourceDictionaryModel>();
            foreach (var theme in themeresources.ThemeDictionaries)
            {
                var themeModel = new ResourceDictionaryModel { Key = theme.Key as string };
                var themeName = theme.Key;
                XmlNode nodeList = root.SelectSingleNode($"/d:ResourceDictionary/d:ResourceDictionary.ThemeDictionaries/d:ResourceDictionary[@x:Key='{themeName}']", manager);
                var value = theme.Value as ResourceDictionary;
                var themeByType = value.GroupBy(g => g.Value.GetType()).ToArray();
                themeModel.Resources = value.Select(t => new DataModel(t, nodeList.ChildNodes.OfType<XmlNode>().Where(node => node.Attributes["x:Key"].Value == t.Key as string).FirstOrDefault())).OrderBy(t => t.Key).ToList();
                themeModels.Add(themeModel);
            }
            Themes.SelectedIndex = -1;
            resources = themeresources.Select(t => new DataModel(t, root.ChildNodes.OfType<XmlNode>().Where(node => node.Attributes["x:Key"]?.Value == t.Key as string).FirstOrDefault())).OrderBy(t=>t.Key).ToList();
            
            Themes.ItemsSource = themeModels;
            if (themeModels.Count > 0)
                Themes.SelectedIndex = 0;
            else 
                UpdateDataView();
        }
        private void Themes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateDataView();
        }

        private void UpdateDataView()
        {
            var items = new List<DataModel>();
            if (resources != null)
                items.AddRange(resources);
            if (Themes.SelectedValue is ResourceDictionaryModel dm)
                items.AddRange(dm.Resources);
            data = items;
            IEnumerable<DataModel> filter = data;
            if (!string.IsNullOrWhiteSpace(filterBox.Text))
                filter = filter.Where(d => d.Key.Contains(filterBox.Text)).OrderBy(t => t.Key);
            if (currentSort == "Key")
            {
                filter = filter.OrderBy(t => t.Key);
            }
            else if (currentSort == "Type")
            {
                filter = filter.OrderBy(t => t.TypeName);
            }
            dg.ItemsSource = filter;
        }

        private List<DataModel> resources;
        private List<DataModel> data;
        private string currentSort;
        private void dg_Sorting(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridColumnEventArgs e)
        {
            if(e.Column.Header?.ToString() == "Key")
            {
                currentSort = "Key";
                dg.ItemsSource = data.OrderBy(t => t.Key);
            }
            else if (e.Column.Header?.ToString() == "Type")
            {
                currentSort = "Type";
                dg.ItemsSource = data.OrderBy(t => t.TypeName);
            }
        }

        private async void TextBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            var text = sender.Text;
            await System.Threading.Tasks.Task.Delay(400);
            if (sender.Text != text) return;
            dg.ItemsSource = data.Where(d => d.Key.Contains(sender.Text)).OrderBy(t=>t.Key);
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            var d = (sender as FrameworkElement).DataContext as DataModel;
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(d.PrettyXml);
            Clipboard.SetContent(dataPackage);
        }

    }
    [Bindable]
    public class ResourceDictionaryModel
    {
        public string Key { get; set; }
        public List<DataModel> Resources { get; set; }
    }
    [Bindable]
    public class DataModel
    {
        public DataModel(KeyValuePair<object, object> kp, XmlNode xml)
        {
            XmlNode = xml;
            Key = kp.Key as string;
            Value = kp.Value;
        }
        public string Key { get; }
        public object Value { get; }
        public string DisplayValue
        {
            get
            {
                if (Value is null) 
                    return "<null>";
                if (Value is SolidColorBrush scb)
                    return scb.Color.ToString();
                if (Value is FontFamily ff)
                    return ff.Source;
                if (Value is Thickness || Value is Boolean || Value is Double || Value is int || Value is string)
                    return Value.ToString();
                return "";
            }
        }
        public string Xml
        {
            get
            {
                return XmlNode?.OuterXml?.Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", "").Replace(" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", "");
            }
        }
        public string PrettyXml
        {
            get
            {
                var xml = XmlNode?.OuterXml;
                if (xml == null)
                    return null;
                return System.Xml.Linq.XElement.Parse(xml).ToString().Replace(" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"", "").Replace(" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"", "");
            }
        }
        public XmlNode XmlNode { get; }
        public string TypeName => Value.GetType().FullName;
    }
    public class DataGridTemplateSelectorColumn : DataGridBoundColumn
    {
        public DataGridTemplateSelectorColumn()
        {
            IsReadOnly = true;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            throw new NotSupportedException();
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var dataTemplate = CellTemplateSelector.SelectTemplate(dataItem);
            ContentControl cc = new ContentControl() { ContentTemplate = dataTemplate };
            if (this.Binding != null)
            {
                cc.SetBinding(ContentControl.ContentProperty, this.Binding);
            }
            //cell.ContentTemplate = dataTemplate;
            //cell.Content = dataItem;
            return cc;
            //cell.ContentTemplateSelector
            
        }

        protected override object PrepareCellForEdit(FrameworkElement editingElement, RoutedEventArgs editingEventArgs)
        {
            throw new NotSupportedException();
        }
        public DataTemplateSelector CellTemplateSelector { get; set; }
    }

    public class ResourceSelector : DataTemplateSelector
    {
        public DataTemplate ColorTemplate { get; set; }
        public DataTemplate BrushTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }
        protected override DataTemplate SelectTemplateCore(object item)
        {
            if (item is DataModel dm)
            {
                item = dm.Value;
                if (item is Windows.UI.Color)
                    return ColorTemplate;
                if (item is Brush)
                    return BrushTemplate;
            }
            return DefaultTemplate;
        }
    }
}
