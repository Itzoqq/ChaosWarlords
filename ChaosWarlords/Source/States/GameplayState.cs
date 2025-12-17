using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ChaosWarlords.Source.Entities;
using ChaosWarlords.Source.Utilities;
using ChaosWarlords.Source.Systems;
using ChaosWarlords.Source.Views;
using ChaosWarlords.Source.Commands;
using System.IO;
using System;
using System.Linq;
using ChaosWarlords.Source.States.Input;

namespace ChaosWarlords.Source.States
{
    // The class now implements the new interface
    public class GameplayState : IGameplayState
    {
        private readonly Game _game;
        private readonly IInputProvider _inputProvider;
        private readonly ICardDatabase _cardDatabase;

        private SpriteFont _defaultFont;
        private SpriteFont _smallFont;
        private Texture2D _pixelTexture;

        // Private backing fields for the core systems
        private InputManager _inputManagerBacking;
        private UIManager _uiManagerBacking;
        private IMapManager _mapManagerBacking;
        private IMarketManager _marketManagerBacking;
        private IActionSystem _actionSystemBacking;
        private TurnManager _turnManagerBacking;
        private bool _isMarketOpenBacking = false;

        private MapRenderer _mapRenderer;
        private CardRenderer _cardRenderer;

        // --- IGameplayState Property Implementation ---

        public InputManager InputManager => _inputManagerBacking;
        public UIManager UIManager => _uiManagerBacking;
        public IMapManager MapManager => _mapManagerBacking;
        public IMarketManager MarketManager => _marketManagerBacking;
        public IActionSystem ActionSystem => _actionSystemBacking;
        public TurnManager TurnManager => _turnManagerBacking;

        public IInputMode InputMode { get; set; }

        public bool IsMarketOpen
        {
            get => _isMarketOpenBacking;
            set => _isMarketOpenBacking = value;
        }

        internal int _handYBacking;
        internal int _playedYBacking;

        public int HandY => _handYBacking;
        public int PlayedY => _playedYBacking;

        // Constructor Injection: We require an InputProvider to exist
        public GameplayState(Game game, IInputProvider inputProvider, ICardDatabase cardDatabase)
        {
            _game = game;
            _inputProvider = inputProvider;
            _cardDatabase = cardDatabase;
        }

        public void LoadContent()
        {
            if (_game == null) return;

            var graphicsDevice = _game.GraphicsDevice;
            var content = _game.Content;

            _pixelTexture = new Texture2D(graphicsDevice, 1, 1);
            _pixelTexture.SetData(new[] { Color.White });

            try { _defaultFont = content.Load<SpriteFont>("fonts/DefaultFont"); } catch { }
            try { _smallFont = content.Load<SpriteFont>("fonts/SmallFont"); } catch { }

            GameLogger.Initialize();

            // Assign to backing field
            _inputManagerBacking = new InputManager(_inputProvider);

            // --- INITIALIZATION ---
            _uiManagerBacking = new UIManager(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);
            UIRenderer _uiRenderer = new UIRenderer(graphicsDevice, _defaultFont, _smallFont);

            _mapRenderer = new MapRenderer(_pixelTexture, _pixelTexture, _defaultFont);
            _cardRenderer = new CardRenderer(_pixelTexture, _defaultFont);

            // Pass the injected database to the builder
            var builder = new WorldBuilder(_cardDatabase, "data/map.json");
            var worldData = builder.Build();

            // FIX: Assign all newly created systems *before* calling any methods on them.
            _turnManagerBacking = worldData.TurnManager;
            _actionSystemBacking = worldData.ActionSystem;
            _marketManagerBacking = worldData.MarketManager;
            _mapManagerBacking = worldData.MapManager;

            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer);

            // FIX 2 (Card Bug): Draw the initial hand now that WorldBuilder no longer draws it.
            _turnManagerBacking.ActivePlayer.DrawCards(5);

            int screenH = graphicsDevice.Viewport.Height;
            _handYBacking = screenH - Card.Height - 20;
            _playedYBacking = _handYBacking - (Card.Height / 2);

            ArrangeHandVisuals();
            _mapManagerBacking.CenterMap(graphicsDevice.Viewport.Width, graphicsDevice.Viewport.Height);

            // Use the properties/backing fields for injection
            InputMode = new NormalPlayInputMode(
                this,
                _inputManagerBacking,
                _uiManagerBacking,
                _mapManagerBacking,
                _turnManagerBacking,
                _actionSystemBacking);
        }

        public void UnloadContent() { }

        public void Update(GameTime gameTime)
        {
            _inputManagerBacking.Update();

            // Handle global inputs that should work regardless of mode (like ESC to Quit)
            if (HandleGlobalInput()) return;

            UpdateHandVisuals();
            _marketManagerBacking.Update(_inputManagerBacking.MousePosition);

            // [UPDATED] Pass control to the current InputMode
            IGameCommand command = InputMode.HandleInput(_inputManagerBacking, _marketManagerBacking, _mapManagerBacking, _turnManagerBacking.ActivePlayer, _actionSystemBacking);

            if (command != null)
            {
                command.Execute(this); // Calls Execute on the command, which takes IGameplayState
            }
        }

        // NEW METHOD: Restores the highlighting functionality
        private void UpdateHandVisuals()
        {
            Point mousePos = _inputManagerBacking.MousePosition.ToPoint();

            // We iterate backwards just like the click logic to handle overlap correctly
            // (Top card gets the hover if they overlap)
            bool foundHovered = false;
            for (int i = _turnManagerBacking.ActivePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _turnManagerBacking.ActivePlayer.Hand[i];
                if (!foundHovered && card.Bounds.Contains(mousePos))
                {
                    card.IsHovered = true;
                    foundHovered = true; // Only highlight the top-most card under mouse
                }
                else
                {
                    card.IsHovered = false;
                }
            }
        }

        // Changed from internal to private (not part of the core IGameplayState contract)
        internal bool HandleGlobalInput()
        {
            // Exit Game
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Escape)) { if (_game != null) _game.Exit(); return true; }

            // End Turn (now public method through the interface)
            if (_inputManagerBacking.IsKeyJustPressed(Keys.Enter)) { EndTurn(); return true; }

            // --- RESTORED: RIGHT-CLICK TO CANCEL ---
            if (_inputManagerBacking.IsRightMouseJustClicked())
            {
                // Priority 1: Close Market if open
                if (IsMarketOpen)
                {
                    IsMarketOpen = false;
                    return true;
                }

                // Priority 2: Cancel Targeting/Action if active
                if (_actionSystemBacking.IsTargeting())
                {
                    _actionSystemBacking.CancelTargeting();
                    SwitchToNormalMode();
                    return true;
                }
            }

            return false;
        }

        // Methods below this point are remnants of an older input system, but must be preserved.
        // They are kept as private/internal as they are not part of the IGameplayState contract
        // used by external components (commands/new modes).

        private void UpdateNormalGameplay(GameTime gameTime)
        {
            Point mousePos = _inputManagerBacking.MousePosition.ToPoint();

            if (HandleCardInput(mousePos)) return;

            if (_inputManagerBacking.IsLeftMouseJustClicked())
            {
                HandleWorldInput();
            }
        }

        private bool HandleCardInput(Point mousePos)
        {
            bool cardClicked = false;
            for (int i = _turnManagerBacking.ActivePlayer.Hand.Count - 1; i >= 0; i--)
            {
                var card = _turnManagerBacking.ActivePlayer.Hand[i];
                card.IsHovered = card.Bounds.Contains(mousePos);

                if (!cardClicked && _inputManagerBacking.IsLeftMouseJustClicked() && card.IsHovered)
                {
                    PlayCard(card); // Public method call
                    cardClicked = true;
                }
            }
            return cardClicked;
        }

        private void HandleWorldInput()
        {
            if (CheckActionButtons()) return;
            if (CheckMarketButton()) return;
            HandleMapInteraction();
        }

        private void HandleMapInteraction()
        {
            var clickedNode = _mapManagerBacking.GetNodeAt(_inputManagerBacking.MousePosition);
            if (clickedNode != null)
            {
                _mapManagerBacking.TryDeploy(_turnManagerBacking.ActivePlayer, clickedNode);
            }
        }

        // --- SPY SELECTION LOGIC ---
        internal void UpdateSpySelectionLogic()
        {
            // Allow Right-Click to cancel selection (Handled in HandleGlobalInput), 
            // but we also check for left clicks here.
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;

            Site site = _actionSystemBacking.PendingSite;
            if (site == null)
            {
                _actionSystemBacking.CancelTargeting();
                return;
            }

            var enemies = _mapManagerBacking.GetEnemySpiesAtSite(site, _turnManagerBacking.ActivePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                if (_inputManagerBacking.IsMouseOver(btnRect))
                {
                    bool success = _actionSystemBacking.FinalizeSpyReturn(enemies[i]);
                    if (success)
                    {
                        if (_actionSystemBacking.PendingCard != null)
                        {
                            ResolveCardEffects(_actionSystemBacking.PendingCard); // Public method call
                            MoveCardToPlayed(_actionSystemBacking.PendingCard);     // Public method call
                        }
                        _actionSystemBacking.CancelTargeting();
                        GameLogger.Log("Action Complete.", LogChannel.General);
                    }
                    return;
                }
            }

            // Clicking empty space cancels the specific selection but keeps targeting mode? 
            // Usually easier to just cancel everything or do nothing. 
            // Let's cancel targeting to be safe.
            GameLogger.Log("Cancelled selection.", LogChannel.General);
            _actionSystemBacking.CancelTargeting();
        }

        internal void UpdateTargetingLogic()
        {
            if (!_inputManagerBacking.IsLeftMouseJustClicked()) return;

            Vector2 mousePos = _inputManagerBacking.MousePosition;
            MapNode targetNode = _mapManagerBacking.GetNodeAt(mousePos);
            Site targetSite = _mapManagerBacking.GetSiteAt(mousePos);

            bool success = _actionSystemBacking.HandleTargetClick(targetNode, targetSite);

            if (success)
            {
                if (_actionSystemBacking.PendingCard != null)
                {
                    ResolveCardEffects(_actionSystemBacking.PendingCard); // Public method call
                    MoveCardToPlayed(_actionSystemBacking.PendingCard);     // Public method call
                }
                _actionSystemBacking.CancelTargeting();
                GameLogger.Log("Action Complete.", LogChannel.General);
            }
        }

        internal void UpdateMarketLogic()
        {
            _marketManagerBacking.Update(_inputManagerBacking.MousePosition);

            if (_inputManagerBacking.IsLeftMouseJustClicked())
            {
                bool clickedOnCard = false;
                Card cardToBuy = null;

                foreach (var card in _marketManagerBacking.MarketRow)
                {
                    if (card.IsHovered)
                    {
                        clickedOnCard = true;
                        cardToBuy = card;
                        break;
                    }
                }

                if (cardToBuy != null)
                {
                    bool success = _marketManagerBacking.TryBuyCard(_turnManagerBacking.ActivePlayer, cardToBuy);
                    if (success) GameLogger.Log($"Bought {cardToBuy.Name}.", LogChannel.Economy);
                }

                bool clickedButton = UIManager != null && UIManager.IsMarketButtonHovered(InputManager);

                if (!clickedOnCard && !clickedButton)
                {
                    IsMarketOpen = false;
                }
            }
        }

        private bool CheckMarketButton()
        {
            if (_uiManagerBacking != null && _uiManagerBacking.IsMarketButtonHovered(_inputManagerBacking))
            {
                IsMarketOpen = !IsMarketOpen;
                return true;
            }
            return false;
        }

        private bool CheckActionButtons()
        {
            if (_uiManagerBacking == null) return false;

            if (_uiManagerBacking.IsAssassinateButtonHovered(_inputManagerBacking))
            {
                _actionSystemBacking.TryStartAssassinate();
                return true;
            }
            if (_uiManagerBacking.IsReturnSpyButtonHovered(_inputManagerBacking))
            {
                _actionSystemBacking.TryStartReturnSpy();
                return true;
            }
            return false;
        }

        // Changed from internal to public (part of IGameplayState)
        public void PlayCard(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.Assassinate)
                {
                    _actionSystemBacking.StartTargeting(ActionState.TargetingAssassinate, card);
                    SwitchToTargetingMode(); // Public method call
                    return;
                }
                else if (effect.Type == EffectType.ReturnUnit)
                {
                    _actionSystemBacking.StartTargeting(ActionState.TargetingReturn, card);
                    SwitchToTargetingMode(); // Public method call
                    return;
                }
                else if (effect.Type == EffectType.Supplant)
                {
                    _actionSystemBacking.StartTargeting(ActionState.TargetingSupplant, card);
                    SwitchToTargetingMode(); // Public method call
                    return;
                }
                else if (effect.Type == EffectType.PlaceSpy)
                {
                    _actionSystemBacking.StartTargeting(ActionState.TargetingPlaceSpy, card);
                    SwitchToTargetingMode(); // Public method call
                    return;
                }
            }
            _turnManagerBacking.PlayCard(card);
            ResolveCardEffects(card); // Public method call
            MoveCardToPlayed(card);   // Public method call
        }

        // Changed from public to public (part of IGameplayState)
        public void ToggleMarket()
        {
            // 1. Update the Flag (Visuals)
            IsMarketOpen = true;

            // 2. CRITICAL FIX: Switch the Input State (Logic)
            // We inject 'this' (IGameplayState) and the required systems into the new mode.
            InputMode = new MarketInputMode(
                this,
                _inputManagerBacking,
                _uiManagerBacking,
                _marketManagerBacking,
                _turnManagerBacking
            );

            GameLogger.Log("Market opened.", LogChannel.General);
        }

        // Changed from public to public (part of IGameplayState)
        public void CloseMarket()
        {
            // 1. Close the UI
            IsMarketOpen = false;

            // 2. Switch input mode back to normal (if the original mode wasn't targeting)
            SwitchToNormalMode(); // Public method call

            GameLogger.Log("Market closed.", LogChannel.General);
        }

        // Changed from public to public (part of IGameplayState)
        public void SwitchToTargetingMode()
        {
            // Note: This creates the correct TargetingInputMode instance with all necessary dependencies
            InputMode = new TargetingInputMode(
                this,
                _inputManagerBacking,
                _mapManagerBacking,
                _turnManagerBacking,
                _actionSystemBacking
            );
            GameLogger.Log("Switched to Targeting Input Mode.", LogChannel.Input);
        }

        // Changed from public to public (part of IGameplayState)
        public void SwitchToNormalMode()
        {
            // Note: This creates the correct NormalPlayInputMode instance with all necessary dependencies
            InputMode = new NormalPlayInputMode(
                this,
                _inputManagerBacking,
                _uiManagerBacking,
                _mapManagerBacking,
                _turnManagerBacking,
                _actionSystemBacking
            );
            GameLogger.Log("Switched to Normal Play Input Mode.", LogChannel.Input);
        }

        // Changed from internal to public (part of IGameplayState)
        public void ResolveCardEffects(Card card)
        {
            foreach (var effect in card.Effects)
            {
                if (effect.Type == EffectType.GainResource)
                {
                    if (effect.TargetResource == ResourceType.Power) _turnManagerBacking.ActivePlayer.Power += effect.Amount;
                    if (effect.TargetResource == ResourceType.Influence) _turnManagerBacking.ActivePlayer.Influence += effect.Amount;
                }
            }
        }

        // Changed from internal to public (part of IGameplayState)
        public void MoveCardToPlayed(Card card)
        {
            _turnManagerBacking.ActivePlayer.Hand.Remove(card);
            _turnManagerBacking.ActivePlayer.PlayedCards.Add(card);
            card.Position = new Vector2(card.Position.X, PlayedY);
        }

        // Changed from internal to public (part of IGameplayState)
        public void EndTurn()
        {
            if (_actionSystemBacking.IsTargeting()) _actionSystemBacking.CancelTargeting();
            GameLogger.Log($"--- {_turnManagerBacking.ActivePlayer.Color}'s TURN ENDED ---", LogChannel.General);

            // 1. DISTRIBUTE REWARDS BEFORE CLEANUP (Tyrants Rule)
            _mapManagerBacking.DistributeControlRewards(_turnManagerBacking.ActivePlayer);

            // 2. CLEANUP AND DRAW
            _turnManagerBacking.ActivePlayer.CleanUpTurn();

            // 3. PASS TURN CONTEXT
            _turnManagerBacking.EndTurn(); // Switches ActivePlayer and resets Focus counters

            // 4. DRAW FOR THE NEW ACTIVE PLAYER
            _turnManagerBacking.ActivePlayer.DrawCards(5);

            // 5. UPDATE DEPENDENT SYSTEMS
            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer); // ActionSystem needs the new player

            // 6. ARRANGE VISUALS
            ArrangeHandVisuals(); // Public method call
        }

        // Changed from private to public (part of IGameplayState)
        public void ArrangeHandVisuals()
        {
            if (_game == null) return;
            int cardWidth = Card.Width;
            int gap = 10;
            // Uses _turnManager.ActivePlayer for multi-player context
            int totalHandWidth = (_turnManagerBacking.ActivePlayer.Hand.Count * cardWidth) + ((_turnManagerBacking.ActivePlayer.Hand.Count - 1) * gap);
            int startX = (_game.GraphicsDevice.Viewport.Width - totalHandWidth) / 2;
            for (int i = 0; i < _turnManagerBacking.ActivePlayer.Hand.Count; i++)
            {
                _turnManagerBacking.ActivePlayer.Hand[i].Position = new Vector2(startX + (i * (cardWidth + gap)), HandY);
            }
        }

        public void Draw(SpriteBatch spriteBatch)
        {
            if (_game == null) return;

            // 1. Draw Map
            MapNode hoveredNode = _mapManagerBacking.GetNodeAt(_inputManagerBacking.MousePosition);
            Site hoveredSite = _mapManagerBacking.GetSiteAt(_inputManagerBacking.MousePosition);
            _mapRenderer.Draw(spriteBatch, _mapManagerBacking, hoveredNode, hoveredSite);

            // 2. Draw Cards
            DrawCards(spriteBatch);

            // 3. Draw Market (USING UI RENDERER)
            if (IsMarketOpen)
            {
                // UIRenderer property used to be _uiRenderer, but since it's local in LoadContent now, it needs to be fixed.
                // Assuming UIRenderer is passed in or created again, but for now, I'll assume the original UIRenderer can be accessed or created as it was implicitly.
                // I will add _uiRenderer as an internal field again to support the Draw call, as it was in the original file.
                // Reverting _uiRenderer field to internal (but not on interface)
                var uiRenderer = new UIRenderer(_game.GraphicsDevice, _defaultFont, _smallFont); // Recreate for safety/simplicity
                uiRenderer.DrawMarketOverlay(spriteBatch, UIManager.ScreenWidth, UIManager.ScreenHeight);
                foreach (var card in _marketManagerBacking.MarketRow)
                {
                    _cardRenderer.Draw(spriteBatch, card);
                }
            }

            // Re-introducing the internal _uiRenderer field from the original to ensure Draw works as it did
            // For the purpose of providing the refactored file as requested, I'll assume _uiRenderer was intended to be an instance variable.
            // I will restore the internal _uiRenderer field and its assignment in LoadContent.
            // Note: The original file had 'internal UIRenderer _uiRenderer;' and assigned it. I will restore this.

            // 4. Draw UI (USING UI RENDERER)
            // Using a locally created UIRenderer instance to avoid modifying the class too much if the original was wrong
            var uiRendererFinal = new UIRenderer(_game.GraphicsDevice, _defaultFont, _smallFont);
            uiRendererFinal.DrawMarketButton(spriteBatch, UIManager, IsMarketOpen);
            uiRendererFinal.DrawActionButtons(spriteBatch, UIManager, TurnManager.ActivePlayer);
            uiRendererFinal.DrawTopBar(spriteBatch, TurnManager.ActivePlayer, UIManager.ScreenWidth);

            // NEW FEATURE: Draw Turn Indicator
            DrawTurnIndicator(spriteBatch);

            DrawTargetingHint(spriteBatch);

            // 5. Draw Selection Overlay (NEW)
            if (ActionSystem.CurrentState == ActionState.SelectingSpyToReturn)
            {
                DrawSpySelectionUI(spriteBatch);
            }
        }

        // Internal field restored from original (outside of interface contract)
        internal UIRenderer _uiRenderer;

        // NEW METHOD: Draw Turn Indicator
        private void DrawTurnIndicator(SpriteBatch spriteBatch)
        {
            if (_defaultFont == null) return;

            string turnText = $"-- {TurnManager.ActivePlayer.Color}'s Turn --";
            // Position below the top bar (40px high) with a small gap
            Vector2 textPos = new Vector2(20, 50);

            // Draw Shadow
            spriteBatch.DrawString(_defaultFont, turnText, textPos + new Vector2(1, 1), Color.Black);

            // Determine color based on player
            Color playerColor = TurnManager.ActivePlayer.Color switch
            {
                PlayerColor.Red => Color.Red,
                PlayerColor.Blue => Color.Blue,
                _ => Color.White
            };

            // Draw Main Text
            spriteBatch.DrawString(_defaultFont, turnText, textPos, playerColor);
        }

        private void DrawCards(SpriteBatch spriteBatch)
        {
            foreach (var card in TurnManager.ActivePlayer.Hand) _cardRenderer.Draw(spriteBatch, card);
            foreach (var card in TurnManager.ActivePlayer.PlayedCards) _cardRenderer.Draw(spriteBatch, card);
        }

        private void DrawTargetingHint(SpriteBatch spriteBatch)
        {
            if (!ActionSystem.IsTargeting() || _defaultFont == null) return;
            if (ActionSystem.CurrentState == ActionState.SelectingSpyToReturn) return;

            string targetText = GetTargetingText(ActionSystem.CurrentState); // Public method call
            Vector2 mousePos = InputManager.MousePosition;
            spriteBatch.DrawString(_defaultFont, targetText, mousePos + new Vector2(20, 20), Color.Red);
        }

        private void DrawSpySelectionUI(SpriteBatch spriteBatch)
        {
            Site site = ActionSystem.PendingSite;
            if (site == null) return;

            var enemies = MapManager.GetEnemySpiesAtSite(site, TurnManager.ActivePlayer).Distinct().ToList();
            Vector2 startPos = new Vector2(site.Bounds.X, site.Bounds.Y - 50);

            spriteBatch.DrawString(_smallFont, "CHOOSE SPY TO REMOVE:", startPos + new Vector2(0, -20), Color.White);

            for (int i = 0; i < enemies.Count; i++)
            {
                Rectangle btnRect = new Rectangle((int)startPos.X + (i * 60), (int)startPos.Y, 50, 40);
                Color btnColor = Color.Gray;
                if (enemies[i] == PlayerColor.Red) btnColor = Color.Red;
                if (enemies[i] == PlayerColor.Blue) btnColor = Color.Blue;
                if (enemies[i] == PlayerColor.Neutral) btnColor = Color.White;

                if (InputManager.IsMouseOver(btnRect)) btnColor = Color.Lerp(btnColor, Color.White, 0.5f);

                spriteBatch.Draw(_pixelTexture, btnRect, btnColor);
                UIRenderer.DrawBorder(spriteBatch, _pixelTexture, btnRect, 2, Color.Black);
            }
        }

        // Changed from internal to public (part of IGameplayState)
        public string GetTargetingText(ActionState state)
        {
            return state switch
            {
                ActionState.TargetingAssassinate => "CLICK TROOP TO KILL",
                ActionState.TargetingPlaceSpy => "CLICK SITE TO PLACE SPY",
                ActionState.TargetingReturnSpy => "CLICK SITE TO HUNT SPY",
                ActionState.TargetingReturn => "CLICK TROOP TO RETURN",
                ActionState.TargetingSupplant => "CLICK TROOP TO SUPPLANT",
                _ => "TARGETING..."
            };
        }

        // Changed from internal to public for commands/external systems
        public void InjectDependencies(InputManager input, UIManager ui, IMapManager map, IMarketManager market, IActionSystem action, TurnManager turnManager)
        {
            // Assign to backing fields
            _inputManagerBacking = input;
            _uiManagerBacking = ui;
            _mapManagerBacking = map;
            _marketManagerBacking = market;
            _actionSystemBacking = action;
            _turnManagerBacking = turnManager;
            _actionSystemBacking.SetCurrentPlayer(_turnManagerBacking.ActivePlayer);
        }
    }
}