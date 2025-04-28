
using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Xaml.Behaviors;

namespace DraggableBehavior
{
    public enum DragMoveKind
    {
        Insert,
        Swap,
    }

    public class DraggableBehavior : Behavior<ItemsControl>
    {
        private List<IndexOffsetAndDistance>? _offsetTestResult;

        private bool _isDragging;
        private Point _mouseDownPosition;
        private DependencyObject? _hitElement;
        private ImageSource? _snapshotOfDraggingElement;
        private Vector _offsetFromElementToMouse;

        private int _hitElementIndex = -1;
        private int _dragToIndex = -1;

        private AdornerLayer? _currentAdornerLayer;
        private Adorner? _currentAdorner;

        public MouseButton DragButton { get; set; } = MouseButton.Left;
        public DragMoveKind DragMoveKind { get; set; } = DragMoveKind.Insert;

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.PreviewMouseDown += AssociatedObject_PreviewMouseDown;
            AssociatedObject.PreviewMouseMove += AssociatedObject_PreviewMouseMove;
            AssociatedObject.PreviewMouseUp += AssociatedObject_PreviewMouseUp;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
        }

        private ImageSource TakeSnapshot(UIElement element)
        {
            // 计算元素相对于其容器的偏移量
            var offset = default(Point);

            if (element is FrameworkElement frameworkElement &&
                VisualTreeHelper.GetParent(element) is Visual visualParent)
            {
                offset = frameworkElement
                    .TransformToAncestor(visualParent)
                    .Transform(new Point(0, 0));
            }

            var dpiScaleX = 1.0;
            var dpiScaleY = 1.0;
            if (PresentationSource.FromVisual(element) is { } presentationSource)
            {
                dpiScaleX = presentationSource.CompositionTarget.TransformToDevice.M11;
                dpiScaleY = presentationSource.CompositionTarget.TransformToDevice.M22;
            }

            // 创建与元素实际大小相同的 RenderTargetBitmap
            var renderWidth = (int)(element.RenderSize.Width * dpiScaleX);
            var renderHeight = (int)(element.RenderSize.Height * dpiScaleY);

            var rtb = new RenderTargetBitmap(
                renderWidth,
                renderHeight,
                96 * dpiScaleX, 96 * dpiScaleY,
                PixelFormats.Pbgra32);

            // 创建一个 DrawingVisual 来正确渲染元素
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                var vb = new VisualBrush(element)
                {
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    ViewboxUnits = BrushMappingMode.Absolute,
                    Viewbox = new Rect(offset, new Size(element.RenderSize.Width, element.RenderSize.Height))
                };

                dc.DrawRectangle(vb, null, new Rect(0, 0, element.RenderSize.Width, element.RenderSize.Height));
            }

            rtb.Render(dv);
            return rtb;
        }

        private void AssociatedObject_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton != DragButton ||
                !CanSwap() ||
                e.OriginalSource is not DependencyObject element ||
                ItemsControl.ContainerFromElement(AssociatedObject, element) is not DependencyObject draggingObject)
            {
                return;
            }

            _mouseDownPosition = e.GetPosition(null);
            _hitElement = draggingObject;
            _hitElementIndex = AssociatedObject.ItemContainerGenerator.IndexFromContainer(draggingObject);
        }

        private void AssociatedObject_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_hitElement is null)
            {
                return;
            }

            if (!_isDragging)
            {
                var mousePosition = e.GetPosition(null);

                if (mousePosition == _mouseDownPosition ||
                    e.OriginalSource is not DependencyObject element ||
                    ItemsControl.ContainerFromElement(AssociatedObject, element) is not DependencyObject draggingObject ||
                    !ReferenceEquals(draggingObject, _hitElement))
                {
                    return;
                }

                if (draggingObject is UIElement draggingUIElement)
                {
                    _isDragging = AssociatedObject.CaptureMouse();

                    Debug.WriteLine("Start Drag");

                    _snapshotOfDraggingElement = TakeSnapshot(draggingUIElement as FrameworkElement);
                    _offsetFromElementToMouse = (Vector)e.GetPosition(draggingUIElement);

                    if (AdornerLayer.GetAdornerLayer(AssociatedObject) is AdornerLayer adornerLayer)
                    {
                        _currentAdornerLayer = adornerLayer;
                        _currentAdorner = new DraggingElementSnapshotAdorner(draggingUIElement, _snapshotOfDraggingElement, _offsetFromElementToMouse);

                        _currentAdornerLayer.Add(_currentAdorner);

                        Debug.WriteLine("Add Adorner");
                    }
                }
                else
                {
                    _isDragging = true;
                }

                Debug.WriteLine($"IsDragging: {_isDragging}");
            }
            else
            {
                Debug.WriteLine($"Drag Moving...");

                if (_currentAdorner is not null)
                {
                    _currentAdorner.InvalidateVisual();
                }

                if (AssociatedObject is not Selector selector ||
                    selector.SelectedIndex == _hitElementIndex ||
                    selector.SelectedIndex == -1)
                {
                    _offsetTestResult ??= new List<IndexOffsetAndDistance>();
                    _offsetTestResult.Clear();

                    for (int i = 0; i < AssociatedObject.Items.Count; i++)
                    {
                        var container = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(i);

                        if (container is UIElement containerElement)
                        {
                            var offset = (Vector)e.GetPosition(containerElement) - _offsetFromElementToMouse;

                            _offsetTestResult.Add(new IndexOffsetAndDistance(i, offset, offset.Length));
                        }
                    }

                    if (_offsetTestResult.Count == 0)
                    {
                        return;
                    }

                    _dragToIndex = _offsetTestResult
                        .OrderBy(v => v.Distance)
                        .Select(v => v.Index)
                        .First();
                }
                else
                {
                    _dragToIndex = selector.SelectedIndex;
                }
            }

            e.Handled = true;
        }

        private void AssociatedObject_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            AssociatedObject.ReleaseMouseCapture();

            if (_isDragging &&
                _dragToIndex >= 0 &&
                _hitElementIndex >= 0)
            {
                if (DragMoveKind == DragMoveKind.Swap)
                {
                    Swap(_hitElementIndex, _dragToIndex, true);
                }
                else
                {
                    Insert(_hitElementIndex, _dragToIndex, true);
                }

                e.Handled = true;
                Debug.WriteLine("Stop Drag");
            }

            _isDragging = false;
            _hitElement = null;
            _snapshotOfDraggingElement = null;

            _hitElementIndex = 0;
            _dragToIndex = 0;

            if (_currentAdornerLayer is not null &&
                _currentAdorner is not null)
            {
                _currentAdornerLayer.Remove(_currentAdorner);
            }
        }

        private bool CanSwap()
        {
            return
                AssociatedObject.ItemsSource is null or IList;
        }

        private void Swap(int index1, int index2, bool notify)
        {
            Debug.WriteLine($"Swap {index1} ~ {index2}");

            if (AssociatedObject.ItemsSource is null)
            {
                (AssociatedObject.Items[index1], AssociatedObject.Items[index2]) =
                    (AssociatedObject.Items[index2], AssociatedObject.Items[index1]);
            }
            else
            {
                if (AssociatedObject.ItemsSource is IList listItemsSource)
                {
                    (listItemsSource[index1], listItemsSource[index2]) =
                        (listItemsSource[index2], listItemsSource[index1]);
                }

                if (notify &&
                    AssociatedObject.ItemsSource is not INotifyCollectionChanged)
                {
                    AssociatedObject.Items.Refresh();
                }
            }
        }

        private void Insert(int index1, int index2, bool notify)
        {
            if (index2 > index1)
            {
                for (int i = index1; i < index2; i++)
                {
                    Swap(i, i + 1, notify && i == index2 - 1);
                }
            }
            else if (index1 > index2)
            {
                for (int i = index1; i > index2; i--)
                {
                    Swap(i, i - 1, notify && i == index2 + 1);
                }
            }
        }

        private record struct IndexOffsetAndDistance(int Index, Vector Offset, double Distance);
        private record struct DependencyPropertyAndValue(DependencyProperty Property, object Value);
    }

}
