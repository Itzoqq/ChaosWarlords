using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;

namespace ChaosWarlords
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private Texture2D _pixelTexture;
        private SpriteFont _defaultFont; 

        // SYSTEMS
        private MapManager _mapManager;
        
        // STATE
        private Player _activePlayer; // <--- NEW: Using the Player class
        private bool _wasMousePressed = false; // To prevent clicking through card onto map

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = Content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }

            GameLogger.Initialize();

            // --- SETUP PLAYER ---
            _activePlayer = new Player(PlayerColor.Red);
            
            // --- FIX: Add Soldiers FIRST so we have Power in hand ---
            for(int i=0; i<3; i++) _activePlayer.Deck.Add(CardFactory.CreateSoldier(_pixelTexture));
            for(int i=0; i<7; i++) _activePlayer.Deck.Add(CardFactory.CreateNoble(_pixelTexture));
            
            // Draw opening hand
            _activePlayer.DrawCards(5);
            ArrangeHandVisuals(); 

            // --- SETUP MAP ---
            var nodes = MapFactory.CreateTestMap(_pixelTexture);
            _mapManager = new MapManager(nodes);
            _mapManager.PixelTexture = _pixelTexture;
        }

        protected override void UnloadContent()
        {
            GameLogger.Log("Session Ended. Flushing logs.", LogChannel.General);
            GameLogger.FlushToFile();
            base.UnloadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape)) Exit();

            var mouseState = Mouse.GetState();
            bool isClicking = mouseState.LeftButton == ButtonState.Pressed;
            bool clickHandled = false;

            // 1. UPDATE HAND (Click to Play)
            for (int i = _activePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _activePlayer.Hand[i];
                card.Update(gameTime, mouseState);

                if (isClicking && !_wasMousePressed && card.IsHovered)
                {
                    PlayCard(card);
                    clickHandled = true; 
                    break; 
                }
            }

            // 2. FIX: UPDATE PLAYED CARDS (So highlights don't get stuck)
            foreach (var card in _activePlayer.PlayedCards)
            {
                card.Update(gameTime, mouseState);
            }

            // 3. UPDATE MAP (Click to Deploy)
            if (!clickHandled)
            {
                _mapManager.Update(mouseState, _activePlayer);
            }

            // 4. FIX: DEBUG UI IN WINDOW TITLE
            // If the font fails, this ensures you still know your stats
            Window.Title = $"ChaosWarlords | Power: {_activePlayer.Power} | Influence: {_activePlayer.Influence} | Deck: {_activePlayer.Deck.Count}";

            _wasMousePressed = isClicking;
            base.Update(gameTime);
        }

        private void ArrangeHandVisuals()
        {
            int startX = 100;
            int gap = 160;
            for (int i = 0; i < _activePlayer.Hand.Count; i++)
            {
                _activePlayer.Hand[i].Position = new Vector2(startX + (i * gap), 500);
            }
        }

        private void PlayCard(Card card)
        {
            GameLogger.Log($"Played card: {card.Name}", LogChannel.Combat);

            // Move from Hand to Played
            _activePlayer.Hand.Remove(card);
            _activePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(100 + (_activePlayer.PlayedCards.Count * 50), 300); // Move to "Played Area"

            // EXECUTE EFFECTS
            foreach(var effect in card.Effects)
            {
                if(effect.Type == EffectType.GainResource)
                {
                    if(effect.TargetResource == ResourceType.Power) 
                        _activePlayer.Power += effect.Amount;
                    if(effect.TargetResource == ResourceType.Influence) 
                        _activePlayer.Influence += effect.Amount;
                }
            }
 
            // Re-arrange hand to fill the gap
            ArrangeHandVisuals();
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.DarkSlateBlue); 
            _spriteBatch.Begin();

            // 1. Draw Map (Bottom Layer)
            _mapManager.Draw(_spriteBatch);

            // 2. Draw Played Cards (Middle Layer)
            foreach (var card in _activePlayer.PlayedCards) card.Draw(_spriteBatch, _defaultFont);

            // 3. Draw Hand (Top Layer)
            foreach (var card in _activePlayer.Hand) card.Draw(_spriteBatch, _defaultFont);

            // 4. Draw UI Overlay (Very Top)
            if(_defaultFont != null)
            {
                // Draw a small background box for the UI so it's readable
                _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 1280, 40), Color.Black * 0.5f);
                
                _spriteBatch.DrawString(_defaultFont, $"Power: {_activePlayer.Power}", new Vector2(20, 10), Color.Orange);
                _spriteBatch.DrawString(_defaultFont, $"Influence: {_activePlayer.Influence}", new Vector2(150, 10), Color.Cyan);
                _spriteBatch.DrawString(_defaultFont, $"Deck: {_activePlayer.Deck.Count}", new Vector2(300, 10), Color.White);
            }

            _spriteBatch.End();
            base.Draw(gameTime);
        }
    }
}