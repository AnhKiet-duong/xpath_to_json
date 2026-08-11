using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using Microsoft.Win32;
using XPathScanner.Core.Models;
using XPathScanner.Core.Services;

namespace XPathScanner.App
{
    public enum DropPosition { Before, After, Into }

    public partial class MainWindow : System.Windows.Window
    {
        private readonly ProcessListService _processListService = new();
        private readonly UiScannerService _scannerService = new();
        private readonly JsonMergeService _jsonMergeService = new();

        private UiNode? _lastScanResult;

        // ---- Trạng thái chọn phần tử làm gốc (hotkey) ----
        private HwndSource? _hwndSource;
        private bool _pickActive;
        private int _pickProcessId;
        private const int HotKeyId = 0x9001;
        private const int WmHotKey = 0x0312;
        private const uint ModControl = 0x0002;
        private const uint VkR = 0x52;

        // ---- Trạng thái kéo-thả ----
        private TreeViewItem? _dragSourceItem;
        private Point _dragStartPoint;
        private TreeViewItem? _dropTargetItem;
        private DropPosition _dropPosition;

        public MainWindow()
        {
            InitializeComponent();
            RefreshButton_Click(this, new RoutedEventArgs());
        }

        // ================= Win32 (hotkey + con trỏ) =================
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            _hwndSource = (HwndSource?)PresentationSource.FromVisual(this);
            _hwndSource?.AddHook(HwndHook);
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmHotKey && wParam.ToInt32() == HotKeyId)
            {
                handled = true;
                CaptureElementUnderCursor();
            }
            return IntPtr.Zero;
        }

        // ================= Sự kiện UI =================
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            var apps = _processListService.GetRunningApps();
            AppComboBox.ItemsSource = apps;
            Log($"Tìm thấy {apps.Count} ứng dụng đang chạy.");
        }

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppComboBox.SelectedItem is not RunningAppInfo selected)
            {
                Log("Vui lòng chọn một ứng dụng trước khi quét.");
                return;
            }

            if (string.IsNullOrWhiteSpace(ScreenNameTextBox.Text))
            {
                Log("Vui lòng nhập Tên màn hình trước khi quét.");
                return;
            }

            try
            {
                Log($"Đang quét: {selected.WindowTitle} ...");
                _lastScanResult = _scannerService.ScanApplication(
                    selected.ProcessId,
                    ScreenNameTextBox.Text.Trim(),
                    RootAnchorTextBox.Text.Trim());

                foreach (var w in _scannerService.Warnings)
                    Log("Cảnh báo: " + w);

                if (!string.IsNullOrWhiteSpace(RootAnchorTextBox.Text) && !_scannerService.LastScanUsedRootAnchor)
                {
                    Log("⚠️ CẢNH BÁO: Bạn đã nhập Root anchor path nhưng lần quét này KHÔNG dùng được " +
                        "nó (xem chi tiết lỗi ở log phía trên). Kết quả quét có thể KHÔNG đúng như mong đợi.");
                }
                else if (_scannerService.LastScanUsedRootAnchor)
                {
                    Log("✅ Đã quét đúng phạm vi Root anchor path đã nhập.");
                }

                RenderTree(_lastScanResult);
                Log("Quét hoàn tất.");
            }
            catch (Exception ex)
            {
                Log("Lỗi khi quét: " + ex.Message);
            }
        }

        private void SaveNewButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScanResult == null)
            {
                Log("Chưa có dữ liệu quét. Hãy bấm Quét trước.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                FileName = ScreenNameTextBox.Text.Trim() + ".json"
            };

            if (dialog.ShowDialog() == true)
            {
                _jsonMergeService.Save(_lastScanResult, dialog.FileName);
                Log("Đã lưu: " + dialog.FileName);
            }
        }

        private void UpdateExistingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_lastScanResult == null)
            {
                Log("Chưa có dữ liệu quét. Hãy bấm Quét trước.");
                return;
            }

            var openDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openDialog.ShowDialog() != true) return;

            var oldResult = _jsonMergeService.LoadIfExists(openDialog.FileName);
            var merged = _jsonMergeService.Merge(oldResult, _lastScanResult);
            _lastScanResult = merged;

            _jsonMergeService.Save(merged, openDialog.FileName);
            _jsonMergeService.SaveDiff(openDialog.FileName); // BƯỚC 9: xuất .diff.json
            Log("Đã cập nhật file: " + openDialog.FileName);
            Log($"Diff: thêm {_jsonMergeService.AddedNodes.Count}, đổi path {_jsonMergeService.ChangedPaths.Count}, node cũ không match {_jsonMergeService.UnmatchedOldNodes.Count}.");

            RenderTree(merged);

            // BƯỚC 9: đề xuất dọn node lỗi thời
            if (_jsonMergeService.UnmatchedOldNodes.Count > 0)
            {
                var cleanup = new CleanupDialog(_jsonMergeService.UnmatchedOldNodes) { Owner = this };
                if (cleanup.ShowDialog() == true && cleanup.SelectedNodes.Count > 0)
                {
                    int removed = _jsonMergeService.RemoveNodes(merged, cleanup.SelectedNodes);
                    _jsonMergeService.Save(merged, openDialog.FileName);
                    RenderTree(merged);
                    Log($"Đã xoá {removed} node lỗi thời và ghi lại file.");
                }
            }
        }

        // ================= Chọn phần tử làm gốc (BƯỚC 9) =================
        private void PickAnchorButton_Click(object sender, RoutedEventArgs e)
        {
            if (AppComboBox.SelectedItem is not RunningAppInfo selected)
            {
                Log("Vui lòng chọn ứng dụng trước khi chọn phần tử gốc.");
                return;
            }

            if (_pickActive)
            {
                UnregisterHotKey();
                Log("Đã huỷ chọn phần tử gốc.");
                return;
            }

            if (_hwndSource == null || _hwndSource.Handle == IntPtr.Zero)
            {
                Log("Cửa sổ chưa sẵn sàng để đăng ký hotkey.");
                return;
            }

            if (RegisterHotKey(_hwndSource.Handle, HotKeyId, ModControl, VkR))
            {
                _pickActive = true;
                _pickProcessId = selected.ProcessId;
                PickAnchorButton.Content = "Huỷ chọn gốc";
                Log("Chế độ chọn gốc: di chuyển chuột lên phần tử trong app đích, nhấn Ctrl+R để chọn.");
            }
            else
            {
                Log("Không đăng ký được hotkey Ctrl+R (có thể bị app khác chiếm).");
            }
        }

        private void CaptureElementUnderCursor()
        {
            UnregisterHotKey(); // chỉ chọn 1 lần
            try
            {
                GetCursorPos(out var pt);
                using var automation = new UIA3Automation();
                var element = automation.FromPoint(new System.Drawing.Point(pt.X, pt.Y));
                if (element == null)
                {
                    Log("Không lấy được phần tử dưới con trỏ.");
                    return;
                }

                var app = FlaUI.Core.Application.Attach(_pickProcessId);
                var mainWindow = app.GetMainWindow(automation);
                if (mainWindow == null)
                {
                    Log("Không tìm thấy cửa sổ chính của app đã chọn.");
                    return;
                }

                int procId = element.Properties.ProcessId.ValueOrDefault;
                if (procId != 0 && procId != _pickProcessId)
                {
                    Log($"Phần tử dưới con trỏ thuộc process {procId}, không phải {_pickProcessId} — bỏ qua.");
                    return;
                }

                string path = UiPathService.BuildRelativePath(mainWindow, element);
                RootAnchorTextBox.Text = path;
                Log("Đã điền root anchor path: " + path);
            }
            catch (Exception ex)
            {
                Log("Lỗi khi chọn phần tử: " + ex.Message);
            }
            finally
            {
                _pickActive = false;
                PickAnchorButton.Content = "Chọn gốc (Ctrl+R)";
            }
        }

        private void UnregisterHotKey()
        {
            if (_hwndSource != null && _hwndSource.Handle != IntPtr.Zero)
                UnregisterHotKey(_hwndSource.Handle, HotKeyId);
            _pickActive = false;
            PickAnchorButton.Content = "Chọn gốc (Ctrl+R)";
        }

        // ================= Tham số hoá {} (BƯỚC 9) =================
        private void ParameterizeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem item || item.Tag is not TreeNodeTag tag)
            {
                Log("Hãy chọn 1 node trong cây trước khi Tham số hoá.");
                return;
            }

            var node = tag.Node;

            if (string.IsNullOrWhiteSpace(node.Path))
            {
                Log("Node này không có path (node hành động logic) — không thể tham số hoá.");
                return;
            }

            // Tham số hoá @AutomationId / @Name CUỐI CÙNG trong path (segment sâu nhất của node)
            var m = Regex.Match(node.Path, @"(@(?:AutomationId|Name))=\\?[""']([^""']+)\\?[""']\s*$");
            if (!m.Success)
            {
                Log("Path không chứa @AutomationId/@Name để tham số hoá.");
                return;
            }

            node.Path = node.Path.Substring(0, m.Index) + m.Groups[1].Value + "=\"{}\"";
            item.Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})";
            Log("Đã tham số hoá: " + node.Path);
        }

        // ================= Đặt node đang chọn làm Root (BƯỚC 9) =================
        private void SetAsRootButton_Click(object sender, RoutedEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem selectedItem)
            {
                Log("Vui lòng chọn 1 node trên cây kết quả trước khi bấm nút này.");
                return;
            }

            if (selectedItem.Tag is not TreeNodeTag tag)
            {
                Log("Không đọc được thông tin node đã chọn.");
                return;
            }

            if (string.IsNullOrEmpty(tag.FullPath))
            {
                Log("Node này không có path riêng (là node nhóm logic, path rỗng) — " +
                    "không thể dùng làm Root anchor. Hãy chọn 1 node khác có path cụ thể.");
                return;
            }

            RootAnchorTextBox.Text = tag.FullPath;

            Log($"Đã điền Root anchor path từ node '{tag.Node.Name}':\n{tag.FullPath}\n" +
                "→ Bấm 'Quét' để quét lại (lấy dữ liệu mới nhất) bắt đầu từ node này.");
        }

        // ================= Dọn node lỗi thời (BƯỚC 9) =================
        private void CleanupButton_Click(object sender, RoutedEventArgs e)
        {
            if (_jsonMergeService.UnmatchedOldNodes.Count == 0)
            {
                Log("Không có node lỗi thời nào để dọn (chạy 'Cập nhật JSON hiện có' trước nếu cần).");
                return;
            }

            if (_lastScanResult == null) return;

            var dialog = new CleanupDialog(_jsonMergeService.UnmatchedOldNodes) { Owner = this };
            if (dialog.ShowDialog() == true && dialog.SelectedNodes.Count > 0)
            {
                int removed = _jsonMergeService.RemoveNodes(_lastScanResult, dialog.SelectedNodes);
                RenderTree(_lastScanResult);
                Log($"Đã xoá {removed} node lỗi thời (bấm Lưu JSON mới / Cập nhật để ghi file).");
            }
        }

        // ================= Kéo-thả sắp xếp cây (BƯỚC 9) =================
        private void ResultTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceItem = FindTreeViewItemFromPoint(e.GetPosition(ResultTreeView));
            _dragStartPoint = e.GetPosition(this);
        }

        private void ResultTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceItem == null) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            Point pos = e.GetPosition(this);
            if (Math.Abs(pos.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(pos.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            if (_dragSourceItem.Tag is not TreeNodeTag dragTag) return;
            var dragged = dragTag.Node;

            _dragSourceItem = null; // drag bắt đầu; drop sẽ xử lý bằng data
            DragDrop.DoDragDrop(ResultTreeView, dragged, DragDropEffects.Move);
        }

        private void ResultTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;

            if (e.Data.GetData(typeof(UiNode)) is not UiNode) return;

            var item = FindTreeViewItemFromPoint(e.GetPosition(ResultTreeView));
            _dropTargetItem = item;
            if (item == null) return;

            _dropPosition = GetDropPosition(item, e.GetPosition(item));
            item.IsSelected = true; // phản hồi trực quan
            e.Effects = DragDropEffects.Move;
        }

        private void ResultTreeView_DragLeave(object sender, DragEventArgs e)
        {
            _dropTargetItem = null;
        }

        private void ResultTreeView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetData(typeof(UiNode)) is not UiNode dragged) return;
            if (_dropTargetItem?.Tag is not TreeNodeTag targetTag) return;
            var target = targetTag.Node;
            if (dragged == target) return;
            if (IsAncestor(dragged, target)) // không cho thả vào chính con của nó
            {
                Log("Không thể thả node vào chính nhánh con của nó.");
                return;
            }

            MoveNode(dragged, target, _dropPosition);
            _lastScanResult ??= dragged;
            RenderTree(_lastScanResult);
            Log($"Đã di chuyển node '{dragged.Name}'.");
        }

        private void MoveNode(UiNode node, UiNode target, DropPosition pos)
        {
            var nodeParent = FindParent(_lastScanResult, node);
            nodeParent?.Children.Remove(node);

            if (pos == DropPosition.Into)
            {
                target.Children.Add(node);
                return;
            }

            var targetParent = FindParent(_lastScanResult, target);
            if (targetParent == null)
            {
                _lastScanResult?.Children.Add(node);
                return;
            }

            int idx = targetParent.Children.IndexOf(target);
            if (idx < 0)
            {
                targetParent.Children.Add(node);
                return;
            }

            int insert = pos == DropPosition.Before ? idx : idx + 1;
            if (insert > targetParent.Children.Count) insert = targetParent.Children.Count;
            targetParent.Children.Insert(insert, node);
        }

        private DropPosition GetDropPosition(TreeViewItem item, Point posInItem)
        {
            double h = item.ActualHeight;
            if (h <= 0) return DropPosition.Into;
            if (posInItem.Y < h / 3) return DropPosition.Before;
            if (posInItem.Y > h * 2 / 3) return DropPosition.After;
            return DropPosition.Into;
        }

        private TreeViewItem? FindTreeViewItemFromPoint(Point p)
        {
            var hit = VisualTreeHelper.HitTest(ResultTreeView, p);
            if (hit == null) return null;

            DependencyObject? current = hit.VisualHit;
            while (current != null && current is not TreeViewItem && current != ResultTreeView)
                current = VisualTreeHelper.GetParent(current);

            return current as TreeViewItem;
        }

        private bool IsAncestor(UiNode node, UiNode target)
        {
            if (node == target) return true;
            foreach (var child in node.Children)
                if (IsAncestor(child, target)) return true;
            return false;
        }

        private UiNode? FindParent(UiNode? root, UiNode child)
        {
            if (root == null) return null;
            foreach (var c in root.Children)
            {
                if (c == child) return root;
                var p = FindParent(c, child);
                if (p != null) return p;
            }
            return null;
        }

        // ================= Render + rename =================
        private void RenderTree(UiNode root)
        {
            ResultTreeView.Items.Clear();
            // parentFullPath rỗng vì root luôn là điểm bắt đầu (full path của root = chính
            // Path của root, hoặc rỗng nếu quét toàn bộ cửa sổ không có root anchor)
            ResultTreeView.Items.Add(BuildTreeViewItem(root, parentFullPath: ""));
        }

        private TreeViewItem BuildTreeViewItem(UiNode node, string parentFullPath)
        {
            // Ghép full path: node.Path rỗng → full path = full path của cha (node nhóm logic,
            // không cộng thêm gì). node.Path khác rỗng → nối tiếp vào full path của cha.
            string fullPath = string.IsNullOrEmpty(node.Path)
                ? parentFullPath
                : parentFullPath + node.Path;

            var tag = new TreeNodeTag { Node = node, FullPath = fullPath };

            var item = new TreeViewItem
            {
                Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})",
                Tag = tag
            };

            foreach (var child in node.Children)
                item.Items.Add(BuildTreeViewItem(child, fullPath));

            return item;
        }

        private void ResultTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ResultTreeView.SelectedItem is not TreeViewItem item) return;
            if (item.Tag is not TreeNodeTag tag) return;
            var node = tag.Node;

            var dialog = new RenameDialog(node.Name)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
            {
                node.Name = dialog.NewName.Trim();
                item.Header = string.IsNullOrEmpty(node.Path) ? node.Name : $"{node.Name}  ({node.Path})";
            }
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
            LogTextBox.ScrollToEnd();
        }
    }
}
