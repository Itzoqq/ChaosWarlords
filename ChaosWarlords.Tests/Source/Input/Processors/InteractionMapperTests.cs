using ChaosWarlords.Source.Rendering.ViewModels;
using ChaosWarlords.Source.Core.Interfaces.Rendering;
using ChaosWarlords.Source.Managers;
using ChaosWarlords.Source.Input;
using ChaosWarlords.Source.Entities.Cards;
using ChaosWarlords.Source.Entities.Map;
using ChaosWarlords.Source.Utilities;
using Microsoft.Xna.Framework;
using NSubstitute;

namespace ChaosWarlords.Tests.Source.Input.Processors
{
    [TestClass]
    public class InteractionMapperTests
    {
        private InteractionMapper _mapper = null!;
        private IGameplayView _view = null!;

        [TestInitialize]
        public void Setup()
        {
            // Mock the View using NSubstitute
            _view = Substitute.For<IGameplayView>();

            // Setup List properties to return real lists so we can add to them in tests
            _view.HandViewModels.Returns(new List<CardViewModel>());
            _view.MarketViewModels.Returns(new List<CardViewModel>());
            _view.PlayedViewModels.Returns(new List<CardViewModel>());

            _mapper = new InteractionMapper(_view);
        }

        #region GetHoveredHandCard Tests

        [TestMethod]
        public void GetHoveredHandCard_ReturnsCard_WhenCardIsHovered()
        {
            // Arrange
            var card = new Card("Test Card", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var viewModel = new CardViewModel(card) { IsHovered = true };
            _view.HandViewModels.Add(viewModel);

            // Act
            var result = _mapper.GetHoveredHandCard();

            // Assert
            Assert.AreEqual(card, result);
        }

        [TestMethod]
        public void GetHoveredHandCard_ReturnsNull_WhenNoCardIsHovered()
        {
            // Arrange
            var card = new Card("Test Card", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var viewModel = new CardViewModel(card) { IsHovered = false };
            _view.HandViewModels.Add(viewModel);

            // Act
            var result = _mapper.GetHoveredHandCard();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetHoveredHandCard_ReturnsFirstHovered_WhenMultipleCardsExist()
        {
            // Arrange
            var card1 = new Card("Card 1", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var card2 = new Card("Card 2", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var vm1 = new CardViewModel(card1) { IsHovered = false };
            var vm2 = new CardViewModel(card2) { IsHovered = true };
            _view.HandViewModels.Add(vm1);
            _view.HandViewModels.Add(vm2);

            // Act
            var result = _mapper.GetHoveredHandCard();

            // Assert
            Assert.AreEqual(card2, result);
        }

        [TestMethod]
        public void GetHoveredHandCard_ReturnsNull_WhenHandIsEmpty()
        {
            // Arrange - empty hand

            // Act
            var result = _mapper.GetHoveredHandCard();

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetHoveredMarketCard Tests

        [TestMethod]
        public void GetHoveredMarketCard_ReturnsCard_WhenCardIsHovered()
        {
            // Arrange
            var card = new Card("Market Card", "Test", 2, CardAspect.Blasphemy, 0, 0, 0);
            var viewModel = new CardViewModel(card) { IsHovered = true };
            _view.MarketViewModels.Add(viewModel);

            // Act
            var result = _mapper.GetHoveredMarketCard();

            // Assert
            Assert.AreEqual(card, result);
        }

        [TestMethod]
        public void GetHoveredMarketCard_ReturnsNull_WhenNoCardIsHovered()
        {
            // Arrange
            var card = new Card("Market Card", "Test", 2, CardAspect.Blasphemy, 0, 0, 0);
            var viewModel = new CardViewModel(card) { IsHovered = false };
            _view.MarketViewModels.Add(viewModel);

            // Act
            var result = _mapper.GetHoveredMarketCard();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetHoveredMarketCard_ReturnsNull_WhenMarketIsEmpty()
        {
            // Arrange - empty market

            // Act
            var result = _mapper.GetHoveredMarketCard();

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetHoveredPlayedCard Tests

        [TestMethod]
        public void GetHoveredPlayedCard_ReturnsCard_WhenMouseIsWithinBounds()
        {
            // Arrange
            var card = new Card("Played Card", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var viewModel = new CardViewModel(card);
            viewModel.Position = new Vector2(100, 100);
            _view.PlayedViewModels.Add(viewModel);

            var mockInputProvider = new ChaosWarlords.Tests.MockInputProvider();
            var inputManager = new InputManager(mockInputProvider);
            mockInputProvider.SetMouseState(InputTestHelpers.CreateReleasedMouseState(110, 110));
            inputManager.Update(); // Update to load the mouse state

            // Act
            var result = _mapper.GetHoveredPlayedCard(inputManager);

            // Assert
            Assert.AreEqual(card, result);
        }

        [TestMethod]
        public void GetHoveredPlayedCard_ReturnsNull_WhenMouseIsOutsideBounds()
        {
            // Arrange
            var card = new Card("Played Card", "Test", 1, CardAspect.Warlord, 0, 0, 0);
            var viewModel = new CardViewModel(card);
            viewModel.Position = new Vector2(100, 100);
            _view.PlayedViewModels.Add(viewModel);

            var mockInputProvider = new ChaosWarlords.Tests.MockInputProvider();
            var inputManager = new InputManager(mockInputProvider);
            mockInputProvider.SetMouseState(InputTestHelpers.CreateReleasedMouseState(500, 500));
            inputManager.Update(); // Update to load the mouse state

            // Act
            var result = _mapper.GetHoveredPlayedCard(inputManager);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetHoveredPlayedCard_ReturnsNull_WhenNoPlayedCards()
        {
            // Arrange - empty played cards
            var mockInputProvider = new ChaosWarlords.Tests.MockInputProvider();
            var inputManager = new InputManager(mockInputProvider);
            mockInputProvider.SetMouseState(InputTestHelpers.CreateReleasedMouseState(110, 110));
            inputManager.Update(); // Update to load the mouse state

            // Act
            var result = _mapper.GetHoveredPlayedCard(inputManager);

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region GetClickedSpyReturnButton Tests

        [TestMethod]
        public void GetClickedSpyReturnButton_ReturnsNull_WhenSiteIsNull()
        {
            // Arrange
            var mousePos = new Point(100, 100);

            // Act
            var result = _mapper.GetClickedSpyReturnButton(mousePos, null!, 800);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetClickedSpyReturnButton_ReturnsPlayerColor_WhenClickingOnSpyButton()
        {
            // Arrange
            var site = new CitySite("Test Site", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.AddSpy(PlayerColor.Red);

            // Based on the logic: drawX = (800 - 200) / 2 = 300, startY = 200, yOffset = 40
            // First spy button: Rectangle(300, 240, 200, 30)
            var mousePos = new Point(350, 250); // Inside first spy button

            // Act
            var result = _mapper.GetClickedSpyReturnButton(mousePos, site, 800);

            // Assert
            Assert.AreEqual(PlayerColor.Red, result);
        }

        [TestMethod]
        public void GetClickedSpyReturnButton_ReturnsNull_WhenClickingOutsideButtons()
        {
            // Arrange
            var site = new CitySite("Test Site", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.AddSpy(PlayerColor.Red);

            var mousePos = new Point(100, 100); // Outside button area

            // Act
            var result = _mapper.GetClickedSpyReturnButton(mousePos, site, 800);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void GetClickedSpyReturnButton_ReturnsCorrectSpy_WithMultipleSpies()
        {
            // Arrange
            var site = new CitySite("Test Site", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            site.AddSpy(PlayerColor.Red);
            site.AddSpy(PlayerColor.Blue);

            // Second spy button: yOffset = 40 + 40 = 80, so Rectangle(300, 280, 200, 30)
            var mousePos = new Point(350, 290); // Inside second spy button

            // Act
            var result = _mapper.GetClickedSpyReturnButton(mousePos, site, 800);

            // Assert
            Assert.AreEqual(PlayerColor.Blue, result);
        }

        [TestMethod]
        public void GetClickedSpyReturnButton_ReturnsNull_WhenSiteHasNoSpies()
        {
            // Arrange
            var site = new CitySite("Test Site", ResourceType.Influence, 1, ResourceType.VictoryPoints, 2);
            var mousePos = new Point(350, 250);

            // Act
            var result = _mapper.GetClickedSpyReturnButton(mousePos, site, 800);

            // Assert
            Assert.IsNull(result);
        }

        #endregion
    }
}



