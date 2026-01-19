using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChaosWarlords.Source.Core.Events
{
    public enum InputEventType
    {
        LeftClick,
        RightClick,
        KeyDown,
        KeyUp
    }

    public class InputEventArgs : EventArgs
    {
        public InputEventType Type { get; }
        public Vector2 Position { get; }
        public Keys? Key { get; }

        public InputEventArgs(InputEventType type, Vector2 position, Keys? key = null)
        {
            Type = type;
            Position = position;
            Key = key;
        }
    }
}
