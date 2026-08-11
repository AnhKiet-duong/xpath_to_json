using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using XPathScanner.Core.Models;

namespace XPathScanner.App
{
    // BƯỚC 9: hộp thoại liệt kê các node cũ không match trong lần merge gần nhất.
    // Người dùng tích chọn node muốn xoá; SelectedNodes trả về tập node đã chọn.
    public partial class CleanupDialog : Window
    {
        private readonly List<UiNode> _nodes;

        public CleanupDialog(List<UiNode> nodes)
        {
            InitializeComponent();
            _nodes = nodes;

            foreach (var node in nodes)
            {
                var checkBox = new CheckBox
                {
                    Content = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})",
                    Tag = node,
                    IsChecked = true,
                    Margin = new Thickness(2, 3, 2, 3)
                };
                NodeListBox.Items.Add(checkBox);
            }
        }

        public HashSet<UiNode> SelectedNodes { get; } = new();

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in NodeListBox.Items)
            {
                if (item is CheckBox checkBox)
                    checkBox.IsChecked = true;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedNodes.Clear();
            foreach (var item in NodeListBox.Items)
            {
                if (item is CheckBox { IsChecked: true } checkBox && checkBox.Tag is UiNode node)
                    SelectedNodes.Add(node);
            }
            DialogResult = true;
        }
    }
}
