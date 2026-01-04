using System;
using System.Collections.Generic;

namespace ChaosWarlords.Source.Rendering.UI
{
    public class PopupBuilder
    {
        private string _title = "Popup";
        private string _message = "";
        private List<Popup.PopupButton> _buttons = new();

        public PopupBuilder WithTitle(string title)
        {
            _title = title;
            return this;
        }

        public PopupBuilder WithMessage(string message)
        {
            _message = message;
            return this;
        }

        public PopupBuilder AddButton(string text, Action onClick, bool isDefault = false)
        {
            _buttons.Add(new Popup.PopupButton(text, onClick, isDefault));
            return this;
        }

        public Popup Build()
        {
            if (_buttons.Count == 0)
            {
                // Default close button if none provided
                AddButton("OK", () => { }, true);
            }
            return new Popup(_title, _message, _buttons);
        }
    }
}
