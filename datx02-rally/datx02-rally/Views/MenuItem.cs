﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace datx02_rally.Menus
{
    public enum ItemPositionX { LEFT, CENTER, RIGHT }
    public enum ItemPositionY { TOP, CENTER, BOTTOM }

    public abstract class MenuItem
    {
        public string Identifier { get; set; }
        public string Text { get; set; }
        public ItemPositionX MenuPositionX { get; set; }
        public ItemPositionY MenuPositionY { get; set; }
        public SpriteFont Font { get; set; }
        public Texture2D Background { get; set; }
        public Color FontColor { get; set; }
        public Color FontColorSelected { get; set; }

        private bool selectable = true;
        public bool Selectable 
        {
            get { return selectable; }
            set { selectable = value; }
        }
        private bool enabled = true;
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; Selectable = value; }
        }

        public abstract void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected);

        private Rectangle bounds;
        public Rectangle Bounds 
        {
            get { 
                return new Rectangle(0, 0, bounds.Width, 
                    (Background != null) ? Background.Bounds.Height : (int)Font.MeasureString(Text).Y); 
            }
            set { bounds = value; } 
        }
        public void SetWidth(int width)
        {
            var b = Bounds;
            b.Width = width;
            Bounds = b;
        }
    }

    public abstract class OptionMenuItem : MenuItem
    {
        public abstract bool IsLastOption();

        public abstract bool IsFirstOption();

        public abstract void NextOption();

        public abstract void PreviousOption();

        public abstract string SelectedOption();

        public Texture2D ArrowLeft { get; set; }
        public Texture2D ArrowRight { get; set; }

    }

    public class TextInputMenuItem : MenuItem
    {
        KeyboardState PrevKeyState;
        int FRAMES_PER_CARET_BLINK = 50;
        int caretBlinkFrameCounter = 0;
        bool blink = true;
        private StringBuilder enteredText;
        public string EnteredText
        {
            get { return enteredText.ToString(); }
            set { enteredText.Clear(); enteredText.Append(value); }
        }

        public TextInputMenuItem(string text) : this(text, null) {}

        public TextInputMenuItem(string text, string identifier)
        {
            Text = text;
            Identifier = identifier;
            enteredText = new StringBuilder();

            MenuPositionX = ItemPositionX.CENTER;
            MenuPositionY = ItemPositionY.CENTER;

            PrevKeyState = Keyboard.GetState();
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected)
        {
            if (selected)
            {
                KeyboardState keyState = Keyboard.GetState();
                foreach (var key in keyState.GetPressedKeys())
                {
                    if (PrevKeyState.IsKeyUp(key))
                    {
                        if (keyState.IsKeyDown(Keys.Back) && enteredText.Length > 0)
                            enteredText.Remove(enteredText.Length - 1, 1);
                        else if (!keyState.IsKeyDown(Keys.Space) && enteredText.Length <= 15)
                        {
                            string inputText = GameManager.GetInstance().GetService<InputComponent>().GetKeyText(key);
                            if (keyState.IsKeyDown(Keys.LeftShift) || keyState.IsKeyDown(Keys.RightShift))
                                inputText = inputText.ToUpper();
                            enteredText.Append(inputText);
                        }
                    }
                }
                PrevKeyState = keyState;
            }

            Rectangle b = Bounds;
            b.X = (int)position.X + 5;
            b.Width -= 10;
            b.Y = (int)position.Y;
            if (selected)
            {
                spriteBatch.Draw(Background, b, Color.White);
            }

            Color textColor;
            if (!Enabled)
                textColor = Color.Gray;
            else
                textColor = (selected) ? FontColorSelected : FontColor;

            // Title string
            Vector2 textOffset = Font.MeasureString(Text);
            textOffset /= 2;
            textOffset.X = Bounds.Width / 6;
            textOffset.Y = Bounds.Height / 2 - textOffset.Y;
            spriteBatch.DrawString(Font, Text, position + textOffset, textColor);

            // Caret blinking
            if (selected && ++caretBlinkFrameCounter > FRAMES_PER_CARET_BLINK)
            {
                blink = !blink;
                caretBlinkFrameCounter = 0;
            }

            // Entered string
            string textWithCaret = enteredText.ToString() + "|";
            string textToDraw = blink && selected ? textWithCaret : enteredText.ToString();
            textOffset = Font.MeasureString(textWithCaret);
            textOffset /= 2;
            textOffset.X = Bounds.Width - Bounds.Width * 3 / 12 - textOffset.X;
            textOffset.Y = Bounds.Height / 2 - textOffset.Y;
            spriteBatch.DrawString(Font, textToDraw, position + textOffset, textColor);
            
        }
    }
 

    public class StateActionMenuItem : MenuItem
    {
        public GameState NextState { get; set; }

        public StateActionMenuItem(string text) : this(text, null, null) { }

        public StateActionMenuItem(string text, GameState? nextState) : this(text, nextState, null) { }

        public StateActionMenuItem(string text, GameState? nextState, string identifier)
        {
            this.Text = text;
            this.Identifier = identifier != null ? identifier : text;
            this.NextState = nextState.HasValue ? nextState.Value : GameState.None;

            MenuPositionX = ItemPositionX.CENTER;
            MenuPositionY = ItemPositionY.CENTER;

            FontColor = Color.White;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected)
        {
            position.X += Bounds.Width / 2 - Background.Bounds.Width / 2;
            spriteBatch.Draw(Background, position, Color.White);

            Color textColor;
            if (!Enabled)
                textColor = Color.Gray;
            else
                textColor = (selected) ? FontColorSelected : FontColor;

            Vector2 textOffset = Font.MeasureString(Text);
            textOffset /= 2;
            textOffset.X = Background.Bounds.Width / 2 - textOffset.X;
            textOffset.Y = Background.Bounds.Height / 2 - textOffset.Y + 3;
            spriteBatch.DrawString(Font, Text, position + textOffset, textColor);
        }
    }

    public class ActionMenuItem : MenuItem
    {
        public delegate void Action();
        public Action PerformAction;

        public ActionMenuItem(string text) : this(text, null, null) { }

        public ActionMenuItem(string text, Action action) : this(text, action, null) { }

        public ActionMenuItem(string text, Action action, string identifier)
        {
            this.Text = text;
            this.Identifier = identifier != null ? identifier : text;
            this.PerformAction = action;

            MenuPositionX = ItemPositionX.CENTER;
            MenuPositionY = ItemPositionY.CENTER;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected)
        {

            position.X += Bounds.Width / 2 - Background.Bounds.Width / 2;
            spriteBatch.Draw(Background, position, Color.White);

            Color textColor;
            if (!Enabled)
                textColor = Color.Gray;
            else
                textColor = (selected) ? FontColorSelected : FontColor;

            Vector2 textOffset = Font.MeasureString(Text);
            textOffset /= 2;
            textOffset.X = Background.Bounds.Width / 2 - textOffset.X;
            textOffset.Y = Background.Bounds.Height / 2 - textOffset.Y + 3;
            spriteBatch.DrawString(Font, Text, position + textOffset, textColor);
        }
    }

    public class BoolOptionMenuItem : OptionMenuItem<bool>
    {
        public BoolOptionMenuItem(string text) : this(text, null) { }

        public BoolOptionMenuItem(string text, string identifier)
            : base(text, identifier)
        {
            this.AddOption("Off", false).AddOption("On", true);
        }

    }

    public class OptionMenuItem<T> : OptionMenuItem
    {
        public List<Tuple<string,T>> options = new List<Tuple<string, T>>();
        public int selectedOptionIndex = 0;

        public OptionMenuItem(string text) : this(text, null) { }

        public OptionMenuItem(string text, string identifier)
        {
            this.Text = text;
            this.Identifier = identifier != null ? identifier : text;

            MenuPositionX = ItemPositionX.CENTER;
            MenuPositionY = ItemPositionY.CENTER;
        }

        public OptionMenuItem<T> AddOption(string text, T value) 
        {
            options.Add(new Tuple<string, T>(text, value));
            return this;
        }

        public OptionMenuItem<T> SetStartOption(string text)
        {
            int matchingIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Item1 == text)
                {
                    matchingIndex = i;
                    break;
                }
            }
            if (matchingIndex > -1)
                selectedOptionIndex = matchingIndex;
            return this;
        }

        public OptionMenuItem<T> SetStartOption(T value)
        {
            int matchingIndex = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (Equal(options[i].Item2, value))
                {
                    matchingIndex = i;
                    break;
                }
            }
            if (matchingIndex > -1) 
                selectedOptionIndex = matchingIndex;
            return this;
        }

        public override bool IsLastOption() 
        {
            return selectedOptionIndex == options.Count - 1;
        }

        public override bool IsFirstOption()
        {
            return selectedOptionIndex == 0;
        }

        public override void NextOption() 
        {
            selectedOptionIndex = Math.Min(options.Count - 1, selectedOptionIndex + 1);
        }

        public override void PreviousOption()
        {
            selectedOptionIndex = Math.Max(0, selectedOptionIndex - 1);
        }

        public override string SelectedOption()
        {
            return options[selectedOptionIndex].Item1;
        }

        public T SelectedValue()
        {
            return options[selectedOptionIndex].Item2;
        }

        // Hack to get equal to work for both reference and value types. Not needed anymore?
        private bool Equal(T o1, T o2)
        {
            //if (o1 is ValueType)
                return o1.Equals(o2);
            //else
            // return (Object)o1 == (Object)o2;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected)
        {
            Rectangle b = Bounds;
            b.X = (int)position.X + 5;
            b.Width -= 10;
            b.Y = (int)position.Y;
            if (selected)
            {
                spriteBatch.Draw(Background, b, Color.White);
            }

            Color textColor;
            if (!Enabled)
                textColor = Color.Gray;
            else
                textColor = (selected) ? FontColorSelected : FontColor;

            // Title string
            Vector2 textOffset = Font.MeasureString(Text);
            textOffset /= 2;
            textOffset.X = Bounds.Width / 6;
            textOffset.Y = Bounds.Height / 2 - textOffset.Y;

            spriteBatch.DrawString(Font, Text, position + textOffset, textColor);

            // Option string
            textOffset = Font.MeasureString(SelectedOption());
            textOffset /= 2;
            textOffset.X = Bounds.Width - Bounds.Width * 3 / 12 - textOffset.X;
            textOffset.Y = Bounds.Height / 2 - textOffset.Y;
            spriteBatch.DrawString(Font, SelectedOption(), position + textOffset, textColor);

            // Arrows
            Vector2 offset = new Vector2(ArrowLeft.Bounds.Width, ArrowLeft.Bounds.Height);
            offset /= 2;
            offset.X = Bounds.Width - Bounds.Width * 5 / 12 - offset.X;
            offset.Y = Bounds.Height / 2 - offset.Y;
            spriteBatch.Draw(ArrowLeft, position + offset, Color.White);

            offset = new Vector2(ArrowRight.Bounds.Width, ArrowRight.Bounds.Height);
            offset /= 2;
            offset.X = Bounds.Width - Bounds.Width * 1 / 12 - offset.X;
            offset.Y = Bounds.Height / 2 - offset.Y;
            spriteBatch.Draw(ArrowRight, position + offset, Color.White);
        }

    }

    public class TextMenuItem : MenuItem
    {
        private string columnTwoText = null;
        public string ColumnTwoText {
            get { return columnTwoText; }
            set { columnTwoText = value; ColumnTwoEnabled = value != null; }
        }
        public bool ColumnTwoEnabled { get; set; }

        public TextMenuItem(string columnOne, string columnTwo) : this(columnOne, columnTwo, null) { }

        public TextMenuItem(string columnOne, string columnTwo, string identifier)
        {
            this.Text = columnOne;
            this.ColumnTwoText = columnTwo;
            this.Identifier = identifier != null ? identifier : columnOne;
            this.Selectable = false;

            MenuPositionX = ItemPositionX.CENTER;
            MenuPositionY = ItemPositionY.CENTER;
        }

        public override void Draw(SpriteBatch spriteBatch, Vector2 position, bool selected)
        {
            if (Text == null)
                return;
            Rectangle b = Bounds;
            b.X = (int)position.X + 5;
            b.Width -= 10;
            b.Y = (int)position.Y;

            Color textColor;
            if (!Enabled)
                textColor = Color.Gray;
            else
                textColor = (selected) ? FontColorSelected : FontColor;

            // Column one string
            Vector2 textOffset = Font.MeasureString(Text);
            textOffset /= 2;
            textOffset.X = Bounds.Width / 6;
            textOffset.Y = Bounds.Height / 2 - textOffset.Y;

            spriteBatch.DrawString(Font, Text, position + textOffset, textColor);

            // Column two string
            if (ColumnTwoEnabled)
            {
                textOffset = Font.MeasureString(ColumnTwoText);
                textOffset /= 2;
                textOffset.X = Bounds.Width - Bounds.Width * 3 / 12 - textOffset.X;
                textOffset.Y = Bounds.Height / 2 - textOffset.Y;
                spriteBatch.DrawString(Font, ColumnTwoText, position + textOffset, textColor);
            }
        }

    }
}
