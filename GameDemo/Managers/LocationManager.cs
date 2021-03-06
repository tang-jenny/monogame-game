﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameDemo.Characters;
using GameDemo.Engine;
using GameDemo.Events;
using GameDemo.Components;
using GameDemo.Managers;
using GameDemo.Notebook;
using GameDemo.Testimonies;
using GameDemo.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameDemo.Locations
{
    public class SpeechMenu : PopupMenu
    {
        public SpeechMenu(string greeting, Rectangle person, ContentManager content, SpriteFont font) : base(content, font)
        {
            StaticText = greeting;
            Menu = content.Load<Texture2D>("speech");
            Position = new Vector2(person.X, person.Y - MenuHeight);

            ConfirmButtonText = "Talk";
            CancelButtonText = "Ignore";
            ButtonLabels.Add(ConfirmButtonText);
            ButtonLabels.Add(CancelButtonText);
        }
    }

    public class LocationManager : IManager
    {
        private MainCharacter MainCharacter;
        private ContentManager Content;

        private string BGImagePath;
        private Background Background;
        private Texture2D Notebook;
        private Rectangle NotebookRect;
        private Texture2D MapIcon;
        private Rectangle MapIconRect;
        private Rectangle IntervieweeListRect;

        private SpriteFont Arial;
        private AllCharacters CharList;

        private MouseState MouseState;
        private MouseState PrevMouseState;

        private LocationState GState;

        private string SelectedPersonName;
        private Dictionary<string, ClickableTexture> CharPics;
        private Dictionary<string, string> Greetings;
        private List<string> Interviewees;
        private bool IsTransitioning;

        private Vector2 TextOffset;
        private const int MaxInterviewees = 3;

        private SpeechMenu SpeechMenu;
        private ConfirmMenu ConfirmMenu;

        enum LocationState
        {
            Normal,
            ClickedPerson,
            ConfirmedPerson,
            ToNotebook,
            ClickedReturn,
            ConfirmedReturn
        }

        public LocationManager(string pathName)
        {
            BGImagePath = pathName;
            Interviewees = new List<string>();
        }

        public void Reset(GameEngine gameEngine, MainCharacter mainCharacter, ContentManager content)
        {
            if (Interviewees.Count == MaxInterviewees)
            {
                GState = LocationState.ConfirmedReturn;
            }
            else
            {
                GState = LocationState.Normal;
            }
            content.Unload();

            MainCharacter = mainCharacter;
            Content = content;
            IsTransitioning = false;
            Point WindowSize = Game1.GetWindowSize();

            // Load Characters
            String CharPath = Path.Combine(Content.RootDirectory, "characters.txt");
            String CharJSON = File.ReadAllText(CharPath);
            CharList = JsonSerializer.Deserialize<AllCharacters>(CharJSON);

            // Load Case Info
            String CasePath = Path.Combine(Content.RootDirectory, "case" + MainCharacter.CurrentCase + ".txt");
            String CaseJSON = File.ReadAllText(CasePath);
            Case Case = JsonSerializer.Deserialize<Case>(CaseJSON);

            // Visual Elements
            Background = new Background(content, BGImagePath);
            CharPics = new Dictionary<string, ClickableTexture>();
            Greetings = new Dictionary<string, string>();

            Notebook = Content.Load<Texture2D>("notebook_icon");
            NotebookRect = new Rectangle(WindowSize.X - 100, 20, 70, 70);
            MapIcon = Content.Load<Texture2D>("map-icon");
            MapIconRect = new Rectangle(WindowSize.X - 200, 20, 70, 70);

            Arial = content.Load<SpriteFont>("Fonts/Arial");
            SpeechMenu = null;

            int NumSuspects = Case.Suspects.Count;
            Vector2 CharPos = new Vector2(WindowSize.X / 6, WindowSize.Y / 3); // may want to customize position at a given location later
            foreach (string Suspect in Case.Suspects)
            {
                Greetings[Suspect] = CharList.AllChars[Suspect].Greetings[0];
                Texture2D CharTexture = Content.Load<Texture2D>(CharList.AllChars[Suspect].ImagePath);
                CharPics[Suspect] = new ClickableTexture(CharTexture, CharPos);
                CharPos.X += 0.75f * WindowSize.X / NumSuspects;
            }
            IntervieweeListRect = new Rectangle(WindowSize.X / 4, 2 * WindowSize.Y / 3, WindowSize.X / 2, WindowSize.Y / 4);

            TextOffset = new Vector2(0, Arial.MeasureString("A").Y);

            MouseState = Mouse.GetState();
            PrevMouseState = MouseState;
        }

        public void Update(GameEngine gameEngine, GameTime gameTime)
        {
            if (IsTransitioning) return;

            MouseState = Mouse.GetState();

            /*** Update components ***/
            SpeechMenu?.Update(gameTime);
            ConfirmMenu?.Update(gameTime);

            if (PrevMouseState.LeftButton == ButtonState.Pressed && MouseState.LeftButton == ButtonState.Released)
            {
                MouseClicked(MouseState);
            }

            PrevMouseState = MouseState;

            switch (GState)
            {
                case LocationState.Normal:
                    foreach (string CharName in CharPics.Keys)
                    {
                        CharPics[CharName].Update();
                    }
                    break;

                case LocationState.ConfirmedPerson:
                    gameEngine.Push(new InterviewManager(SelectedPersonName), true, true);

                    // Add an entry for the relationship when you meet a character.
                    if (!MainCharacter.Relationships.ContainsKey(SelectedPersonName))
                    {
                        MainCharacter.Relationships[SelectedPersonName] = 0;
                    }
                    IsTransitioning = true;
                    break;

                case LocationState.ToNotebook:
                    int[] Null = new int[] { -1 };
                    gameEngine.Push(new NotebookManager(false, ref Null), true, true);
                    IsTransitioning = true;
                    break;

                case LocationState.ConfirmedReturn:
                    gameEngine.Pop(true, true);
                    IsTransitioning = true;
                    break;

                default:
                    break;
            }

        }

        public void Draw(SpriteBatch spriteBatch, GraphicsDeviceManager graphics)
        {
            // Background
            Background.Draw(spriteBatch, graphics);

            // Banner
            String DateString = MainCharacter.GetDateTimeString() + " - " + BGImagePath;
            DrawingUtils.DrawTextBanner(spriteBatch, graphics, Arial, DateString, Color.Red, Color.Black);
            spriteBatch.Draw(Notebook, NotebookRect, Color.White);
            spriteBatch.Draw(MapIcon, MapIconRect, Color.White);

            // Draw Characters
            foreach (string CharName in CharPics.Keys)
            {
                CharPics[CharName].Draw(spriteBatch, graphics);
            }

            // Draw Interviewee List
            DrawingUtils.DrawFilledRectangle(spriteBatch, graphics, IntervieweeListRect, Color.Beige);
            DrawingUtils.DrawOpenRectangle(spriteBatch, graphics, IntervieweeListRect, Color.Maroon, 3);
            Vector2 TextPos = new Vector2(IntervieweeListRect.X + 10, IntervieweeListRect.Y + 10);
            for (int i = 0; i < MaxInterviewees; i++)
            {
                string Name = "";
                if (Interviewees.Count > i) Name = CharList.AllChars[Interviewees[i]].Name;
                spriteBatch.DrawString(Arial, (i + 1) + ". " + Name, TextPos, Color.Black) ;
                TextPos += TextOffset;
            }

            // Speech Menu if place is clicked
            if (GState == LocationState.ClickedPerson && SpeechMenu != null)
            {
                SpeechMenu.Draw(spriteBatch, graphics);
            }

            // Confirm Menu if returning to map
            if (GState == LocationState.ClickedReturn && ConfirmMenu != null)
            {
                ConfirmMenu.Draw(spriteBatch, graphics);
            }
        }

        private void MouseClicked(MouseState mouseState)
        {
            Point MouseClick = new Point(mouseState.X, mouseState.Y);
            Rectangle MouseClickRect = new Rectangle(MouseClick, new Point(10, 10));

            switch (GState)
            {
                // If nothing selected, check whether location was selected
                case LocationState.Normal:
                    foreach (string CharName in CharPics.Keys)
                    {
                        if (MouseClickRect.Intersects(CharPics[CharName].Rect))
                        {
                            GState = LocationState.ClickedPerson;
                            SpeechMenu = new SpeechMenu(Greetings[CharName], CharPics[CharName].Rect, Content, Arial);
                            SelectedPersonName = CharName;
                        }
                    }
                    if (MouseClickRect.Intersects(NotebookRect))
                    {
                        GState = LocationState.ToNotebook;
                    }
                    if (MouseClickRect.Intersects(MapIconRect))
                    {
                        GState = LocationState.ClickedReturn;
                        string query = "Are you sure you're done exploring for now?";
                        ConfirmMenu = new ConfirmMenu(query, Content, Arial);
                    }
                    break;

                case LocationState.ClickedPerson:
                    if (SpeechMenu.IsCancelling(MouseClickRect))
                    {
                        GState = LocationState.Normal;
                        SpeechMenu = null;
                    }
                    else if (SpeechMenu.IsConfirming(MouseClickRect))
                    {
                        GState = LocationState.ConfirmedPerson;
                        Interviewees.Add(SelectedPersonName);
                        SpeechMenu = null;
                    }
                    break;

                case LocationState.ClickedReturn:
                    if (ConfirmMenu.IsCancelling(MouseClickRect))
                    {
                        GState = LocationState.Normal;
                        ConfirmMenu = null;
                    }
                    else if (ConfirmMenu.IsConfirming(MouseClickRect))
                    {
                        GState = LocationState.ConfirmedReturn;
                        ConfirmMenu = null;
                    }
                    break;

                default:
                    break;
            }
        }
    }
}
