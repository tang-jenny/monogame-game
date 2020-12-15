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

        private SpriteFont Arial;

        private MouseState MouseState;
        private MouseState PrevMouseState;

        private LocationState GState;

        private string SelectedPersonName;
        private Dictionary<string, ClickableTexture> CharPics;
        private Dictionary<string, string> Greetings;
        private Dictionary<string, bool> SpokenWith;
        private bool IsTransitioning;

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
                            if (SpokenWith[CharName]) SpeechMenu.DisableButton(SpeechMenu.ConfirmButtonText);
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
                        SpokenWith[SelectedPersonName] = true;
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

        public LocationManager(string pathName)
        {
            BGImagePath = pathName;
            SpokenWith = new Dictionary<string, bool>(); 
        }

        public void Reset(GameEngine gameEngine, MainCharacter mainCharacter, ContentManager content)
        {
            content.Unload();

            MainCharacter = mainCharacter;
            Content = content;
            IsTransitioning = false;

            // Load Characters
            String CharPath = Path.Combine(Content.RootDirectory, "characters.txt");
            String CharJSON = File.ReadAllText(CharPath);
            AllCharacters CharList = JsonSerializer.Deserialize<AllCharacters>(CharJSON);

            // Load Case Info
            String CasePath = Path.Combine(Content.RootDirectory, "case" + MainCharacter.CurrentCase + ".txt");
            String CaseJSON = File.ReadAllText(CasePath);
            Case Case = JsonSerializer.Deserialize<Case>(CaseJSON);

            // Visual Elements
            Background = new Background(content, BGImagePath);
            CharPics = new Dictionary<string, ClickableTexture>();
            Greetings = new Dictionary<string, string>();

            Notebook = Content.Load<Texture2D>("notebook_icon");
            MapIcon = Content.Load<Texture2D>("map-icon");
            GState = LocationState.Normal;

            Arial = content.Load<SpriteFont>("Fonts/Arial");
            SpeechMenu = null;

            if (BGImagePath == "Castle")
            {
                int NumSuspects = Case.Suspects.Count;
                Vector2 CharPos = new Vector2(200, 400); // may want to customize position at a given location later
                foreach (string Suspect in Case.Suspects)
                {
                    Greetings[Suspect] = CharList.AllChars[Suspect].Greetings[0];
                    Texture2D CharTexture = Content.Load<Texture2D>(CharList.AllChars[Suspect].ImagePath);
                    CharPics[Suspect] = new ClickableTexture(CharTexture, CharPos);
                    if (!SpokenWith.ContainsKey(Suspect)) SpokenWith[Suspect] = false;

                    CharPos.X += 900 / NumSuspects;
                }  
            }
            /***** End Replace *****/

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

            if (GState == LocationState.Normal)
            {
                foreach (string CharName in CharPics.Keys)
                {
                    CharPics[CharName].Update();
                }
            }

            if (PrevMouseState.LeftButton == ButtonState.Pressed && MouseState.LeftButton == ButtonState.Released)
            {
                MouseClicked(MouseState);
            }

            PrevMouseState = MouseState;

            switch (GState)
            {
                case (LocationState.ConfirmedPerson):
                    gameEngine.Push(new EventManager(), true, true);

                    // Add an entry for the relationship when you meet a character.
                    if (!MainCharacter.Relationships.ContainsKey(SelectedPersonName))
                    {
                        MainCharacter.Relationships[SelectedPersonName] = 0;
                    }
                    IsTransitioning = true;
                    break;

                case (LocationState.ToNotebook):
                    gameEngine.Push(new NotebookManager(), true, true);
                    IsTransitioning = true;
                    break;

                case (LocationState.ConfirmedReturn):
                    gameEngine.Pop(true, true);
                    IsTransitioning = true;
                    break;

                default:
                    break;
            }

        }

        public void Draw(GameEngine gameEngine, SpriteBatch spriteBatch, GraphicsDeviceManager graphics)
        {
            // Background
            Background.Draw(spriteBatch, graphics);

            // Banner
            String DateString = MainCharacter.GetDateTimeString() + " - " + BGImagePath;
            DrawingUtils.DrawTextBanner(spriteBatch, graphics, Arial, DateString, Color.Red, Color.Black);

            // Banner Icons
            if (NotebookRect.IsEmpty)
            {
                NotebookRect = new Rectangle(graphics.GraphicsDevice.Viewport.Width - 100, 20, 70, 70);
            }
            spriteBatch.Draw(Notebook, NotebookRect, Color.White);

            if (MapIconRect.IsEmpty)
            {
                MapIconRect = new Rectangle(graphics.GraphicsDevice.Viewport.Width - 200, 20, 70, 70);
            }
            spriteBatch.Draw(MapIcon, MapIconRect, Color.White);

            // Draw Characters
            foreach (string CharName in CharPics.Keys)
            {
                CharPics[CharName].Draw(spriteBatch, graphics);
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
    }
}
