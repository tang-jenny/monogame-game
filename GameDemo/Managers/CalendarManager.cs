﻿using System;
using System.Linq;
using System.IO;
using System.Text.Json;
using GameDemo.Engine;
using GameDemo.Map;
using GameDemo.Notebook;
using GameDemo.Locations;
using GameDemo.Characters;
using GameDemo.Components;
using GameDemo.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;

using System.Collections.Generic;

namespace GameDemo.Managers
{

    public class CalendarManager : IManager
    {
        private Background Background;
        private MainCharacter MainCharacter;
        private ContentManager Content;
        private SpriteFont JustBreathe;
        private SpriteFont Arial;

        private Button ConfirmButton;
        private OptionsList ActivitiesList;
        private Rectangle ActivityRect;
        private OptionsList PeopleList;
        private ClickableTexture Notebook;
        private Calendar Calendar;
        private InfoTable StatsTable;
        private InfoTable RelTable;

        private Case Case;
        private DateTime ThisMonday;

        private CalendarState GState;
        private MouseState MouseState;
        private MouseState PrevMouseState;
        private bool IsTransitioning;

        enum CalendarState
        {
            ActivityChoice,
            ConfirmActivity,
            NextDay,
            ToWeekend,
            ToNotebook
        }

        public CalendarManager()
        {
            IsTransitioning = false;
        }

        public void Reset(GameEngine gameEngine, MainCharacter mainCharacter, ContentManager content)
        {
            if (IsTransitioning)
            {
                GState = CalendarState.ActivityChoice;
                Background = new Background(content, "bulletin");
                IsTransitioning = false;
                return;
            }

            Point WindowSize = Game1.GetWindowSize();
            content.Unload();

            // Common to all Modes
            Background = new Background(content, "bulletin");
            MainCharacter = mainCharacter;
            Content = content;
            JustBreathe = Content.Load<SpriteFont>("Fonts/JustBreathe20");
            Arial = Content.Load<SpriteFont>("Fonts/Arial");
            GState = CalendarState.ActivityChoice;
            MouseState = Mouse.GetState();
            PrevMouseState = MouseState;

            // Load Case Info (will want to create method for this)
            String CasePath = Path.Combine(Content.RootDirectory, "case" + MainCharacter.CurrentCase + ".txt");
            String CaseJSON = File.ReadAllText(CasePath);
            Case = JsonSerializer.Deserialize<Case>(CaseJSON);

            ThisMonday = MainCharacter.GetDate();

            ActivityRect = new Rectangle(WindowSize.X / 2, WindowSize.Y / 2, 500, 300);
            // eventually pull from Activity list json.
            Dictionary<string, string> Activities = new Dictionary<string, string>
                {
                    { "Have a tea party", "charm" },
                    { "Fight a dragon", "courage"},
                    { "Have a chat", "empathy" },
                    { "Go to the library", "intelligence" },
                    { "Pump iron", "strength" },
                    { "Start a business", "money" }
                };
            ActivitiesList = new OptionsList(Activities, JustBreathe,
                new Point(ActivityRect.X + 10, ActivityRect.Y + 30));
            PeopleList = new OptionsList(Case.CharDict, JustBreathe,
                new Point(ActivityRect.X + 4 * ActivityRect.Width / 6, ActivityRect.Y + 30));

            ConfirmButton = null;
            Calendar = new Calendar(new Rectangle(110, 150, 960, 200), JustBreathe, ThisMonday);
            Notebook = new ClickableTexture(Content.Load<Texture2D>("notebook_icon"), new Vector2(WindowSize.X - 100, 20));
        }

        public void Update(GameEngine gameEngine, GameTime gameTime)
        {
            if (IsTransitioning)
            {
                return;
            }

            // Update Dynamic Components
            PeopleList?.Update();
            ActivitiesList?.Update();

            MouseState = Mouse.GetState();
            if (PrevMouseState.LeftButton == ButtonState.Pressed && MouseState.LeftButton == ButtonState.Released)
            {
                MouseClicked(MouseState.X, MouseState.Y);
            }

            switch(GState)
            {
                case CalendarState.NextDay:
                    if (Calendar.IsMoving())
                    {
                        Calendar.Update(gameTime);
                    }
                    else if (Calendar.DayIndex < 5)
                    {
                        GState = CalendarState.ActivityChoice;
                    }
                    else GState = CalendarState.ToWeekend;
                    // update position of box
                    break;

                case CalendarState.ToWeekend:
                    gameEngine.Push(new MapManager(), true, true);
                    MainCharacter.ToWeekend();
                    IsTransitioning = true;
                    break;

                case CalendarState.ToNotebook:
                    int[] Null = new int[] { -1 };
                    gameEngine.Push(new NotebookManager(false, ref Null), true, true);
                    IsTransitioning = true;
                    break;

                case CalendarState.ConfirmActivity:
                    ConfirmButton?.Update();
                    if (PeopleList?.SelectedOption == null || ActivitiesList?.SelectedOption == null)
                    {
                        GState = CalendarState.ActivityChoice;
                    }
                    break;

                case CalendarState.ActivityChoice:
                    if (PeopleList?.SelectedOption != null && ActivitiesList?.SelectedOption != null)
                    {
                        GState = CalendarState.ConfirmActivity;
                        ConfirmButton = new Button("Go!", JustBreathe,
                            new Vector2(ActivityRect.X + ActivityRect.Width / 2,
                            ActivityRect.Y + ActivityRect.Height - 50));
                    }
                    break;

                default:
                    break;
            }
            PrevMouseState = MouseState;
        }

        public void Draw(SpriteBatch spriteBatch, GraphicsDeviceManager graphics)
        {

            Background.Draw(spriteBatch, graphics);

            // Banner
            string WeekString = "Week of " + ThisMonday.ToString("M/d");

            DrawingUtils.DrawTextBanner(spriteBatch, graphics, Arial, WeekString, Color.Red, Color.Black);
            Notebook.Draw(spriteBatch, graphics);

            // create calendar
            Calendar.Draw(spriteBatch, graphics);

            // Confirm activity to move on to the next day
            DrawingUtils.DrawFilledRectangle(spriteBatch, graphics, ActivityRect, Color.Beige);
            DrawingUtils.DrawOpenRectangle(spriteBatch, graphics, ActivityRect, Color.DarkSlateBlue, 3);

            // Formulate Activity - "Today I will [blank] with [blank]"
            spriteBatch.DrawString(JustBreathe, "Today I will ...", new Vector2(ActivityRect.X, ActivityRect.Y), Color.Navy);
            ActivitiesList.Draw(spriteBatch, graphics);
            spriteBatch.DrawString(JustBreathe, "with", new Vector2(ActivityRect.X + 250, ActivityRect.Y + 100), Color.Navy);
            PeopleList.Draw(spriteBatch, graphics);

            // Add tables for current Main Character stats (need to find a different way to update them live 
            string[] Aspects = new string[6] { "charm", "courage", "empathy", "intelligence", "strength", "money" };
            StatsTable = new InfoTable(MainCharacter.Stats, Aspects, new Rectangle(110, 400, 200, 252), JustBreathe);
            string[] People = Case.Suspects.Concat(Case.TestimonyOnly).ToArray();
            RelTable = new InfoTable(MainCharacter.Relationships, People, new Rectangle(350, 400, 200, 252), JustBreathe);
            StatsTable.Draw(spriteBatch, graphics);
            RelTable.Draw(spriteBatch, graphics);

            if (GState == CalendarState.ConfirmActivity)
            {
                ConfirmButton.Draw(spriteBatch, graphics);
            }
        }

        /* MouseClick Handler */
        private void MouseClicked(int x, int y)
        {
            Rectangle mouseClickRect = new Rectangle(x, y, 10, 10);

            switch (GState)
            {
                case CalendarState.ConfirmActivity:
                    if (mouseClickRect.Intersects(ConfirmButton.Rect))
                    {
                        // Later on there will be more complicated effects, keeping basic
                        if (!MainCharacter.Stats.ContainsKey(ActivitiesList.SelectedOption))
                        {
                            MainCharacter.Stats[ActivitiesList.SelectedOption] = 1;
                        }
                        else MainCharacter.Stats[ActivitiesList.SelectedOption]++;

                        if (!MainCharacter.Relationships.ContainsKey(PeopleList.SelectedOption))
                        {
                            MainCharacter.Relationships[PeopleList.SelectedOption] = 1;
                        }
                        else MainCharacter.Relationships[PeopleList.SelectedOption]++;

                        Calendar.AddEntry(ActivitiesList.SelectedLabel + " with " + PeopleList.SelectedLabel);
                        GState = CalendarState.NextDay;
                        Calendar.MoveDay();
                    }

                    if (mouseClickRect.Intersects(Notebook.Rect))
                    {
                        GState = CalendarState.ToNotebook;
                    }
                    break;

                case CalendarState.ActivityChoice:
                    if (mouseClickRect.Intersects(Notebook.Rect))
                    {
                        GState = CalendarState.ToNotebook;
                    }
                    break;

                default:
                    break;
            }
        }

    }
}