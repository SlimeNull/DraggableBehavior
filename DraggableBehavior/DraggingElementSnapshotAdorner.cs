
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace DraggableBehavior
{
    public class DraggingElementSnapshotAdorner : Adorner
    {
        private readonly UIElement _draggingElement;
        private readonly ImageSource _snapshot;
        private readonly Vector _offsetFromElementToMouse;

        public DraggingElementSnapshotAdorner(UIElement draggingElement, ImageSource snapshot, Vector offsetFromElementToMouse) : base(draggingElement)
        {
            ArgumentNullException.ThrowIfNull(draggingElement);
            ArgumentNullException.ThrowIfNull(snapshot);

            _draggingElement = draggingElement;
            _snapshot = snapshot;
            _offsetFromElementToMouse = offsetFromElementToMouse;
        }

        protected override GeometryHitTestResult? HitTestCore(GeometryHitTestParameters hitTestParameters)
        {
            return null;
        }

        protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
        {
            return null;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            var position = Mouse.GetPosition(this);

            drawingContext.DrawImage(_snapshot, new Rect(
                position.X - _offsetFromElementToMouse.X,
                position.Y - _offsetFromElementToMouse.Y,
                _draggingElement.RenderSize.Width,
                _draggingElement.RenderSize.Height));
        }
    }

}
