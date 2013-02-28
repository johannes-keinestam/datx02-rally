﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;

namespace datx02_rally.Menus
{
    public enum ItemPositionX { LEFT, CENTER, RIGHT }
    public enum ItemPositionY { TOP, CENTER, BOTTOM }

    public interface IMenuItem
    {
        string Identifier { get; set; }
        string Text { get; set; }
        ItemPositionX MenuPositionX { get; set; }
        ItemPositionY MenuPositionY { get; set; }
    }

    public interface IOptionMenuItem : IMenuItem
    {
        bool IsLastOption();

        bool IsFirstOption();

        void NextOption();

        void PreviousOption();

        string SelectedOption();

    }

    public class StateActionMenuItem : IMenuItem
    {
        public string Text { get; set; }
        public string Identifier { get; set; }
        public ItemPositionX MenuPositionX { get; set; }
        public ItemPositionY MenuPositionY { get; set; }
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
        }
    }

    public class ActionMenuItem : IMenuItem
    {
        public string Text { get; set; }
        public string Identifier { get; set; }
        public ItemPositionX MenuPositionX { get; set; }
        public ItemPositionY MenuPositionY { get; set; }

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

    public class OptionMenuItem<T> : IOptionMenuItem
    {
        public string text;
        public string Text {
            set { text = value; }
            get { return text + ": " + SelectedOption(); }
        }
        public string Identifier { get; set; }
        public ItemPositionX MenuPositionX { get; set; }
        public ItemPositionY MenuPositionY { get; set; }

        public List<Tuple<string,T>> options = new List<Tuple<string, T>>();
        public int selectedOptionIndex = 0;

        public OptionMenuItem(string text) : this(text, null) { }

        public OptionMenuItem(string text, string identifier)
        {
            this.text = text;
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

        public bool IsLastOption() 
        {
            return selectedOptionIndex == options.Count - 1;
        }

        public bool IsFirstOption()
        {
            return selectedOptionIndex == 0;
        }

        public void NextOption() 
        {
            selectedOptionIndex = Math.Min(options.Count - 1, selectedOptionIndex + 1);
        }

        public void PreviousOption()
        {
            selectedOptionIndex = Math.Max(0, selectedOptionIndex - 1);
        }

        public string SelectedOption()
        {
            return options[selectedOptionIndex].Item1;
        }

        public T SelectedValue()
        {
            return options[selectedOptionIndex].Item2;
        }

        // Hack to get equal to work for both reference and value types
        private bool Equal(T o1, T o2)
        {
            if (o1 is ValueType)
                return o1.Equals(o2);
            else
                return (Object)o1 == (Object)o2;
        }

    }
}