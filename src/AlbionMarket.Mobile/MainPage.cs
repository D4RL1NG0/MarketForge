using System.Text.Json;
using Microsoft.Maui.Dispatching;
using AlbionMarket.Mobile.Models;
using AlbionMarket.Mobile.Services;

namespace AlbionMarket.Mobile;

public sealed class MainPage : ContentPage
{
    private const string BaseUrlPreferenceKey = "AlbionMarketApiBaseUrl";
    private const string DefaultApiBaseUrl = "https://marketforge-api-d4rl1ng0.onrender.com";
    private const string LanguagePreferenceKey = "AlbionMarketLanguage";
    private const string FavoritesPreferenceKey = "AlbionMarketFavoritesV2";
    private const string HistoryPreferenceKey = "AlbionMarketHistoryV2";
    private const string ServerPreferenceKey = "AlbionMarketServer";
    private const long Premium30DaysGoldCost = 3750;

    private readonly AlbionMarketApiClient _apiClient = new();
    private readonly List<Action> _textRefreshers = new();

    private readonly Entry _apiUrlEntry;
    private readonly Button _menuButton;
    private readonly Button _favoriteButton;
    private readonly Button _quickModeButton;
    private readonly Button _categoryModeButton;
    private readonly Picker _languagePicker;
    private readonly Picker _serverPicker;
    private readonly Label _serverTimeHeaderLabel;
    private readonly Label _goldPriceLabel;
    private readonly Label _premiumPriceLabel;
    private IDispatcherTimer? _clockTimer;
    private DateTimeOffset _nextGoldRefreshUtc = DateTimeOffset.MinValue;
    private long _lastGoldPrice;
    private View? _quickSearchCard;
    private View? _categoryMarketCard;
    private VerticalStackLayout? _drawerListLayout;
    private Label? _drawerSectionTitle;

    private readonly Entry _quickSearchEntry;
    private readonly Picker _quickTierPicker;
    private readonly Picker _quickEnchantPicker;
    private readonly Picker _quickQualityPicker;
    private readonly Button _quickSearchButton;
    private readonly VerticalStackLayout _quickSuggestionsLayout;

    private readonly Picker _categoryPicker;
    private readonly Picker _groupPicker;
    private readonly Picker _tierPicker;
    private readonly Picker _enchantPicker;
    private readonly Picker _qualityPicker;
    private readonly Button _reloadCategoriesButton;
    private readonly Button _browseToggleButton;
    private readonly VerticalStackLayout _browseItemsLayout;
    private readonly Label _browseTitleLabel;

    private readonly Label _opportunityTitle;
    private readonly Label _opportunityRoute;
    private readonly Label _opportunityProfit;
    private readonly Label _selectedItemLabel;
    private readonly Label _cacheInfoLabel;
    private readonly Label _refiningFooterLabel;
    private readonly Label _statusLabel;
    private readonly Image _selectedItemIcon;
    private readonly VerticalStackLayout _pricesLayout;
    private View? _marketScreenContent;
    private View? _refiningScreenContent;
    private View? _craftingScreenContent;

    private readonly Picker _refiningResourcePicker;
    private readonly Picker _refiningTierPicker;
    private readonly Picker _refiningEnchantPicker;
    private readonly Button _refiningCaerleonButton;
    private readonly Button _refiningButton;
    private readonly Label _refiningIntroLabel;
    private readonly VerticalStackLayout _refiningResultLayout;

    private readonly Entry _craftingSearchEntry;
    private readonly Picker _craftingTierPicker;
    private readonly Picker _craftingEnchantPicker;
    private readonly Picker _craftingQualityPicker;
    private readonly Button _craftingCaerleonButton;
    private readonly Button _craftingButton;
    private readonly Label _craftingIntroLabel;
    private readonly VerticalStackLayout _craftingSuggestionsLayout;
    private readonly VerticalStackLayout _craftingResultLayout;
    private ItemSuggestion? _selectedCraftItem;
    private int _craftingSearchVersion;
    private bool _updatingCraftingText;
    private VerticalStackLayout? _toolsOptionsLayout;
    private bool _toolsExpanded;
    private bool _includeCaerleonInRefining;
    private bool _includeCaerleonInCrafting;

    private readonly List<CatalogCategory> _categories = new();
    private readonly List<CatalogGroup> _currentGroups = new();
    private readonly List<ItemSuggestion> _visibleItems = new();

    private AppLanguage _language;
    private ItemSuggestion? _selectedItem;
    private SelectionMode _selectionMode = SelectionMode.Category;
    private SelectionMode _activeInputMode = SelectionMode.Category;
    private AppScreen _activeScreen = AppScreen.Market;
    private MarketOpportunity? _lastOpportunity;
    private int _quickSearchVersion;
    private int _browseVersion;
    private bool _updatingQuickText;
    private bool _updatingCatalogPickers;
    private bool _categoriesLoaded;
    private bool _browseItemsExpanded;
    private int _browseItemsCount;
    private Grid? _drawerPanel;
    private BoxView? _drawerShade;
    private bool _drawerOpen;
    private bool _updatingLanguagePicker;
    private bool _updatingServerPicker;

    private enum SelectionMode
    {
        QuickSearch,
        Category
    }

    private enum AppScreen
    {
        Market,
        Refining,
        Crafting
    }

    public MainPage()
    {
        Title = "MarketForge";
        BackgroundColor = Color.FromArgb("#071018");
        _language = Preferences.Get(LanguagePreferenceKey, "pt-BR") == "en" ? AppLanguage.En : AppLanguage.PtBr;

        _menuButton = new Button
        {
            Text = "☰",
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 18,
            FontSize = 24,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#152233"),
            TextColor = Colors.White
        };
        _menuButton.Clicked += async (_, _) => await OpenDrawerAsync();

        _serverTimeHeaderLabel = new Label
        {
            Text = GetServerTimeText(),
            TextColor = Color.FromArgb("#DCEBFF"),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        _quickModeButton = CreateModeButton("⚡ Busca rápida", SelectionMode.QuickSearch);
        _categoryModeButton = CreateModeButton("🛒 Por categoria", SelectionMode.Category);

        _languagePicker = CreatePicker("Idioma", new[] { "🇧🇷 Português", "🇺🇸 English" }, _language == AppLanguage.PtBr ? 0 : 1);
        _languagePicker.SelectedIndexChanged += OnLanguageChanged;

        _serverPicker = CreatePicker("Servidor", new[] { "🌎 Americas (West)", "🌏 Asia (East)", "🌍 Europe" }, ServerIndexFromKey(Preferences.Get(ServerPreferenceKey, "west")));
        _serverPicker.SelectedIndexChanged += OnServerChanged;

        _goldPriceLabel = new Label
        {
            Text = "🟡 Ouro: -  •  🪙 Prata",
            TextColor = Color.FromArgb("#F6D365"),
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _premiumPriceLabel = new Label
        {
            Text = T("💎 Premium 30 dias: -", "💎 30-day premium: -"),
            TextColor = Color.FromArgb("#D8E8FF"),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _apiUrlEntry = CreateEntry("URL da API", Preferences.Get(BaseUrlPreferenceKey, DefaultApiBaseUrl));
        _apiUrlEntry.Keyboard = Keyboard.Url;
        _apiUrlEntry.Completed += async (_, _) => await ReloadCategoriesAsync();

        _reloadCategoriesButton = CreateSecondaryButton("Atualizar catálogo");
        _reloadCategoriesButton.Clicked += async (_, _) => await ReloadCategoriesAsync();

        _quickSearchEntry = CreateEntry("Digite parte do nome. Ex: bolsa, espada, capa...", string.Empty);
        _quickSearchEntry.TextChanged += OnQuickSearchTextChanged;

        _quickTierPicker = CreatePicker("Tier", new[] { "T4", "T5", "T6", "T7", "T8" }, 0);
        _quickEnchantPicker = CreatePicker("Encant.", new[] { ".0", ".1", ".2", ".3", ".4" }, 0);
        _quickQualityPicker = CreatePicker("Qualidade", Array.Empty<string>(), 0);
        PopulateQualityPicker(_quickQualityPicker, 0);
        _quickTierPicker.SelectedIndexChanged += OnQuickFilterChanged;
        _quickEnchantPicker.SelectedIndexChanged += OnQuickFilterChanged;
        _quickQualityPicker.SelectedIndexChanged += OnQuickFilterChanged;

        _quickSearchButton = CreatePrimaryButton("Buscar preços");
        _quickSearchButton.Clicked += async (_, _) =>
        {
            _selectionMode = SelectionMode.QuickSearch;
            await SearchPricesAsync();
        };

        _quickSuggestionsLayout = new VerticalStackLayout { Spacing = 8, IsVisible = false };

        _categoryPicker = CreatePicker("Categoria", Array.Empty<string>(), 0);
        _groupPicker = CreatePicker("Grupo", Array.Empty<string>(), 0);
        _tierPicker = CreatePicker("Tier", new[] { "T4", "T5", "T6", "T7", "T8" }, 0);
        _enchantPicker = CreatePicker("Encant.", new[] { ".0", ".1", ".2", ".3", ".4" }, 0);
        _qualityPicker = CreatePicker("Qualidade", Array.Empty<string>(), 0);
        PopulateQualityPicker(_qualityPicker, 0);

        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        _groupPicker.SelectedIndexChanged += OnGroupChanged;
        _tierPicker.SelectedIndexChanged += OnCategoryFilterChanged;
        _enchantPicker.SelectedIndexChanged += OnCategoryFilterChanged;
        _qualityPicker.SelectedIndexChanged += OnCategoryFilterChanged;

        _browseTitleLabel = new Label
        {
            Text = T("Escolha uma categoria e depois um grupo.", "Choose a category and then a group."),
            TextColor = Color.FromArgb("#A9B8C9"),
            FontSize = 13
        };

        _browseToggleButton = CreateSecondaryButton("▶ Selecione o item");
        _browseToggleButton.IsVisible = false;
        _browseToggleButton.Clicked += (_, _) => ToggleBrowseItems();

        _browseItemsLayout = new VerticalStackLayout { Spacing = 8, IsVisible = false };

        _favoriteButton = new Button
        {
            Text = "♡",
            WidthRequest = 46,
            HeightRequest = 46,
            CornerRadius = 18,
            FontSize = 22,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#202B3D"),
            TextColor = Color.FromArgb("#FF6B96")
        };
        _favoriteButton.Clicked += (_, _) => ToggleFavoriteForSelectedItem();

        _selectedItemIcon = new Image
        {
            Source = ItemIconCacheService.GetBestSource("T4_BAG", 1, 96),
            WidthRequest = 64,
            HeightRequest = 64,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#0D1622")
        };

        _selectedItemLabel = new Label
        {
            Text = T("Nenhum item selecionado", "No item selected"),
            TextColor = Color.FromArgb("#8FA2B8"),
            FontSize = 12
        };

        _opportunityTitle = new Label
        {
            Text = T("Melhor oportunidade", "Best opportunity"),
            TextColor = Colors.White,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold
        };

        _opportunityRoute = new Label
        {
            Text = T("Selecione um item pelo market ou pela busca rápida.", "Select an item from the market or quick search."),
            TextColor = Color.FromArgb("#C8D5E3"),
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _opportunityProfit = new Label
        {
            Text = T("Lucro: -", "Profit: -"),
            TextColor = Color.FromArgb("#4FE18A"),
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        };

        _cacheInfoLabel = new Label
        {
            Text = GetCacheInfoText(),
            TextColor = Color.FromArgb("#8FA2B8"),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
        };

        _pricesLayout = new VerticalStackLayout { Spacing = 10 };

        _refiningResourcePicker = CreatePicker("Material bruto", Array.Empty<string>(), 0);
        PopulateRefiningResourcePicker();
        _refiningTierPicker = CreatePicker("Tier", new[] { "T4", "T5", "T6", "T7", "T8" }, 1);
        _refiningEnchantPicker = CreatePicker("Encant.", new[] { ".0", ".1", ".2", ".3", ".4" }, 0);
        _refiningCaerleonButton = CreateSecondaryButton(string.Empty);
        _refiningCaerleonButton.Clicked += async (_, _) =>
        {
            _includeCaerleonInRefining = !_includeCaerleonInRefining;
            UpdateCaerleonButtons();
            await SearchRefiningAsync();
        };
        _refiningButton = CreatePrimaryButton(T("Refinar", "Refine"));
        _refiningButton.Clicked += async (_, _) => await SearchRefiningAsync();
        _refiningIntroLabel = new Label
        {
            Text = T("Escolha o material, tier e encantamento para simular compra dos materiais necessários, refino e venda do refinado.", "Choose material, tier and enchantment to simulate buying needed materials, refining and selling the refined resource."),
            TextColor = Color.FromArgb("#A9B8C9"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _refiningResultLayout = new VerticalStackLayout { Spacing = 10 };

        _craftingSearchEntry = CreateEntry("Digite o item para craftar. Ex: espada, bolsa, capacete...", string.Empty);
        _craftingSearchEntry.TextChanged += OnCraftingSearchTextChanged;
        _craftingTierPicker = CreatePicker("Tier", new[] { "T4", "T5", "T6", "T7", "T8" }, 0);
        _craftingEnchantPicker = CreatePicker("Encant.", new[] { ".0", ".1", ".2", ".3", ".4" }, 0);
        _craftingQualityPicker = CreatePicker("Qualidade", Array.Empty<string>(), 0);
        PopulateQualityPicker(_craftingQualityPicker, 0);
        _craftingTierPicker.SelectedIndexChanged += OnCraftingFilterChanged;
        _craftingEnchantPicker.SelectedIndexChanged += OnCraftingFilterChanged;
        _craftingQualityPicker.SelectedIndexChanged += OnCraftingFilterChanged;
        _craftingCaerleonButton = CreateSecondaryButton(string.Empty);
        _craftingCaerleonButton.Clicked += async (_, _) =>
        {
            _includeCaerleonInCrafting = !_includeCaerleonInCrafting;
            UpdateCaerleonButtons();
            if (_selectedCraftItem is not null)
                await SearchCraftingAsync();
        };
        _craftingButton = CreatePrimaryButton(T("Calcular craft", "Calculate craft"));
        _craftingButton.Clicked += async (_, _) => await SearchCraftingAsync();
        _craftingIntroLabel = new Label
        {
            Text = T("Pesquise o item, selecione tier, encantamento e qualidade. O app calcula ingredientes baratos, venda do item pronto e lucro líquido.", "Search the item, choose tier, enchantment and quality. The app calculates cheapest ingredients, finished item sale and net profit."),
            TextColor = Color.FromArgb("#A9B8C9"),
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _craftingSuggestionsLayout = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        _craftingResultLayout = new VerticalStackLayout { Spacing = 10 };

        _refiningFooterLabel = new Label
        {
            Text = GetCacheInfoText(),
            TextColor = Color.FromArgb("#8FA2B8"),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
        };

        _statusLabel = new Label
        {
            Text = T("Pronto.", "Ready."),
            TextColor = Color.FromArgb("#8FA2B8"),
            FontSize = 12
        };

        BuildLayout();
        UpdateCaerleonButtons();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartLiveTimers();
        if (!_categoriesLoaded)
            await ReloadCategoriesAsync();
        await RefreshGoldAsync();
    }

    protected override void OnDisappearing()
    {
        _clockTimer?.Stop();
        base.OnDisappearing();
    }

    private void BuildLayout()
    {
        _quickSearchCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                CreateSectionTitle(() => T("Busca rápida", "Quick search"), () => T("Digite o nome quando já souber o item.", "Type the name when you already know the item.")),
                _quickSearchEntry,
                CreateQuickFiltersGrid(),
                _quickSuggestionsLayout,
                _quickSearchButton
            }
        }, Color.FromArgb("#244B68"), Color.FromArgb("#0E1926"));

        _categoryMarketCard = CreateCard(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                CreateSectionTitle(() => T("Market por categoria", "Category market"), () => T("Navegue como no market do Albion.", "Browse like Albion's market.")),
                CreateCatalogGrid(),
                CreateCategoryFiltersGrid(),
                _browseTitleLabel,
                _browseToggleButton,
                _browseItemsLayout
            }
        }, Color.FromArgb("#345B45"), Color.FromArgb("#0E1926"));

        UpdateSearchModeVisibility();

        _marketScreenContent = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                CreateModeSelector(),
                _quickSearchCard,
                _categoryMarketCard,
                CreateOpportunityCard(),
                CreateLocalizedLabel(() => T("Preços por cidade", "Prices by city"), Colors.White, 20, FontAttributes.Bold, margin: new Thickness(0, 8, 0, 0)),
                _pricesLayout,
                _cacheInfoLabel
            }
        };

        _refiningScreenContent = CreateRefiningScreen();
        _craftingScreenContent = CreateCraftingScreen();
        UpdateMainScreenVisibility();

        var mainScroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 14, 16, 24),
                Spacing = 14,
                Children =
                {
                    CreateHero(),
                    _marketScreenContent,
                    _refiningScreenContent,
                    _craftingScreenContent,
                    _statusLabel
                }
            }
        };

        _drawerShade = new BoxView
        {
            BackgroundColor = Colors.Black,
            Opacity = 0,
            IsVisible = false
        };
        var shadeTap = new TapGestureRecognizer();
        shadeTap.Tapped += async (_, _) => await CloseDrawerAsync();
        _drawerShade.GestureRecognizers.Add(shadeTap);

        _drawerPanel = CreateDrawerPanel();
        _drawerPanel.IsVisible = false;
        _drawerPanel.TranslationX = -330;

        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var serverHeader = CreateServerTimeHeader();
        Grid.SetRow(serverHeader, 0);
        Grid.SetRow(mainScroll, 1);
        mainGrid.Children.Add(serverHeader);
        mainGrid.Children.Add(mainScroll);

        var root = new Grid();
        root.Children.Add(mainGrid);
        root.Children.Add(_drawerShade);
        root.Children.Add(_drawerPanel);
        Content = root;
    }

    private View CreateServerTimeHeader()
    {
        return new Border
        {
            BackgroundColor = Color.FromArgb("#081521"),
            Stroke = Color.FromArgb("#1A2A3D"),
            StrokeThickness = 0,
            Padding = new Thickness(12, 6),
            Content = _serverTimeHeaderLabel
        };
    }

    private View CreateHero()
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var title = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = "MarketForge",
                    TextColor = Colors.White,
                    FontSize = 30,
                    FontAttributes = FontAttributes.Bold
                },
                CreateLocalizedLabel(() => T("Rotas, preços e oportunidades sem complicação.", "Routes, prices and opportunities without friction."), Color.FromArgb("#A8BDD3"), 14)
            }
        };

        var appIcon = new Image
        {
            Source = "app_icon_marketforge.png",
            WidthRequest = 46,
            HeightRequest = 46,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center
        };

        Grid.SetColumn(_menuButton, 0);
        Grid.SetColumn(appIcon, 1);
        Grid.SetColumn(title, 2);
        grid.Children.Add(_menuButton);
        grid.Children.Add(appIcon);
        grid.Children.Add(title);
        return grid;
    }

    private View CreateOpportunityCard()
    {
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star)
            }
        };
        Grid.SetColumn(_opportunityTitle, 0);
        header.Children.Add(_opportunityTitle);

        var info = new VerticalStackLayout
        {
            Spacing = 7,
            HorizontalOptions = LayoutOptions.Fill,
            Children =
            {
                header,
                _selectedItemLabel,
                _opportunityRoute,
                new HorizontalStackLayout
                {
                    Spacing = 10,
                    Children = { _opportunityProfit, _favoriteButton }
                }
            }
        };

        var cardGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };
        Grid.SetColumn(_selectedItemIcon, 0);
        Grid.SetColumn(info, 1);
        cardGrid.Children.Add(_selectedItemIcon);
        cardGrid.Children.Add(info);
        return CreateCard(cardGrid, Color.FromArgb("#3D5E83"), Color.FromArgb("#111D2B"));
    }

    private View CreateRefiningScreen()
    {
        var filters = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        Grid.SetColumn(_refiningResourcePicker, 0);
        Grid.SetColumnSpan(_refiningResourcePicker, 2);
        Grid.SetRow(_refiningResourcePicker, 0);
        Grid.SetColumn(_refiningTierPicker, 0);
        Grid.SetRow(_refiningTierPicker, 1);
        Grid.SetColumn(_refiningEnchantPicker, 1);
        Grid.SetRow(_refiningEnchantPicker, 1);
        filters.Children.Add(_refiningResourcePicker);
        filters.Children.Add(_refiningTierPicker);
        filters.Children.Add(_refiningEnchantPicker);

        return new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                CreateCard(new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        CreateSectionTitle(() => T("Sniper de refino", "Refining sniper"), () => T("Compra materiais baratos, simula o refino e compara a venda do refinado.", "Buys cheap materials, simulates refining and compares refined sale.")),
                        _refiningIntroLabel,
                        filters,
                        _refiningCaerleonButton,
                        _refiningButton
                    }
                }, Color.FromArgb("#5A4A24"), Color.FromArgb("#111D2B")),
                _refiningResultLayout,
                _refiningFooterLabel
            }
        };
    }


    private View CreateCraftingScreen()
    {
        var filters = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        Grid.SetColumn(_craftingTierPicker, 0);
        Grid.SetColumn(_craftingEnchantPicker, 1);
        Grid.SetColumn(_craftingQualityPicker, 2);
        filters.Children.Add(_craftingTierPicker);
        filters.Children.Add(_craftingEnchantPicker);
        filters.Children.Add(_craftingQualityPicker);

        return new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                CreateCard(new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        CreateSectionTitle(() => T("Sniper de craft", "Crafting sniper"), () => T("Compra ingredientes baratos, calcula o craft e compara com a melhor cidade de venda.", "Buys cheap ingredients, calculates crafting and compares with the best sale city.")),
                        _craftingIntroLabel,
                        _craftingSearchEntry,
                        filters,
                        _craftingCaerleonButton,
                        _craftingSuggestionsLayout,
                        _craftingButton
                    }
                }, Color.FromArgb("#3B426A"), Color.FromArgb("#111D2B")),
                _craftingResultLayout,
                CreateLocalizedLabel(() => GetCacheInfoText(), Color.FromArgb("#8FA2B8"), 12)
            }
        };
    }

    private static Entry CreateEntry(string placeholder, string text)
    {
        return new Entry
        {
            Text = text,
            Placeholder = placeholder,
            TextColor = Colors.White,
            PlaceholderColor = Color.FromArgb("#73879E"),
            BackgroundColor = Color.FromArgb("#0B1420"),
            FontSize = 14
        };
    }

    private static Picker CreatePicker(string title, IEnumerable<string> items, int selectedIndex)
    {
        var picker = new Picker
        {
            Title = title,
            TextColor = Colors.White,
            TitleColor = Color.FromArgb("#73879E"),
            BackgroundColor = Color.FromArgb("#0B1420"),
            FontSize = 13
        };
        foreach (var item in items)
            picker.Items.Add(item);
        if (picker.Items.Count > 0)
            picker.SelectedIndex = Math.Clamp(selectedIndex, 0, picker.Items.Count - 1);
        return picker;
    }

    private static Button CreatePrimaryButton(string text)
    {
        return new Button
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#4FE18A"),
            TextColor = Color.FromArgb("#06110A"),
            CornerRadius = 16,
            HeightRequest = 52
        };
    }

    private static Button CreateSecondaryButton(string text)
    {
        return new Button
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#152233"),
            TextColor = Color.FromArgb("#DFEAF7"),
            CornerRadius = 14,
            HeightRequest = 46,
            FontSize = 13
        };
    }

    private Button CreateModeButton(string text, SelectionMode mode)
    {
        var button = new Button
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 16,
            HeightRequest = 50,
            FontSize = 14
        };
        button.Clicked += (_, _) =>
        {
            _activeInputMode = mode;
            UpdateSearchModeVisibility();
        };
        return button;
    }

    private View CreateModeSelector()
    {
        var grid = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        Grid.SetColumn(_quickModeButton, 0);
        Grid.SetColumn(_categoryModeButton, 1);
        grid.Children.Add(_quickModeButton);
        grid.Children.Add(_categoryModeButton);
        return grid;
    }

    private void UpdateSearchModeVisibility()
    {
        var quickActive = _activeInputMode == SelectionMode.QuickSearch;
        if (_quickSearchCard is not null)
            _quickSearchCard.IsVisible = quickActive;
        if (_categoryMarketCard is not null)
            _categoryMarketCard.IsVisible = !quickActive;

        _quickModeButton.BackgroundColor = quickActive ? Color.FromArgb("#4FE18A") : Color.FromArgb("#152233");
        _quickModeButton.TextColor = quickActive ? Color.FromArgb("#06110A") : Color.FromArgb("#DFEAF7");
        _categoryModeButton.BackgroundColor = quickActive ? Color.FromArgb("#152233") : Color.FromArgb("#4FE18A");
        _categoryModeButton.TextColor = quickActive ? Color.FromArgb("#DFEAF7") : Color.FromArgb("#06110A");
    }

    private Grid CreateDrawerPanel()
    {
        var panel = new Grid
        {
            WidthRequest = 318,
            HorizontalOptions = LayoutOptions.Start,
            BackgroundColor = Color.FromArgb("#0B1420"),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            Padding = new Thickness(18, 22, 18, 18)
        };

        var content = new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label
                        {
                            Text = "MarketForge",
                            TextColor = Colors.White,
                            FontSize = 24,
                            FontAttributes = FontAttributes.Bold,
                            VerticalTextAlignment = TextAlignment.Center,
                            HorizontalOptions = LayoutOptions.FillAndExpand
                        },
                        CreateDrawerIconButton("✕", async () => await CloseDrawerAsync())
                    }
                },
                CreateDrawerButton(() => "🧰 " + T("Ferramentas", "Tools") + (_toolsExpanded ? "  ▾" : "  ▸"), async () =>
                {
                    ToggleToolsOptions();
                    await Task.CompletedTask;
                }),
                (_toolsOptionsLayout = new VerticalStackLayout
                {
                    Spacing = 8,
                    IsVisible = false,
                    Padding = new Thickness(12, 0, 0, 0),
                    Children =
                    {
                        CreateDrawerButton(() => "🛒 " + T("Mercado", "Market"), async () =>
                        {
                            SetActiveScreen(AppScreen.Market);
                            await CloseDrawerAsync();
                        }),
                        CreateDrawerButton(() => "⚒ " + T("Refino", "Refining"), async () =>
                        {
                            SetActiveScreen(AppScreen.Refining);
                            await CloseDrawerAsync();
                        }),
                        CreateDrawerButton(() => "🛠 " + T("Craft", "Craft"), async () =>
                        {
                            SetActiveScreen(AppScreen.Crafting);
                            await CloseDrawerAsync();
                        })
                    }
                }),
                CreateDrawerButton(() => "♡ " + T("Favoritos", "Favorites"), async () =>
                    await ShowSavedItemsInDrawerAsync(FavoritesPreferenceKey, T("Favoritos", "Favorites"))),
                CreateDrawerButton(() => "🕘 " + T("Histórico", "History"), async () =>
                    await ShowSavedItemsInDrawerAsync(HistoryPreferenceKey, T("Histórico", "History"))),
                new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#213247"), Margin = new Thickness(0, 8) },
                CreateLocalizedLabel(() => T("Idioma", "Language"), Color.FromArgb("#7890A7"), 12, FontAttributes.Bold),
                CreateLanguageSelector(),
                CreateLocalizedLabel(() => T("Servidor de preços", "Price server"), Color.FromArgb("#7890A7"), 12, FontAttributes.Bold),
                _serverPicker,
                CreateCard(new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        CreateLocalizedLabel(() => T("Cotação", "Exchange"), Color.FromArgb("#8FA2B8"), 11, FontAttributes.Bold),
                        _goldPriceLabel,
                        _premiumPriceLabel
                    }
                }, Color.FromArgb("#4C3D21"), Color.FromArgb("#181712")),
                new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#213247"), Margin = new Thickness(0, 8) },
                (_drawerSectionTitle = new Label
                {
                    Text = T("Toque em Favoritos ou Histórico", "Tap Favorites or History"),
                    TextColor = Color.FromArgb("#7890A7"),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold
                }),
                (_drawerListLayout = new VerticalStackLayout { Spacing = 8 })
            }
        };

        var footer = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                CreateLocalizedLabel(() => T("Desenvolvido por", "Developed by"), Color.FromArgb("#7890A7"), 11),
                new Label
                {
                    Text = "D4RL1NG0",
                    TextColor = Color.FromArgb("#4FE18A"),
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold
                },
                new Label
                {
                    Text = "MarketForge • V1.1.0",
                    TextColor = Color.FromArgb("#5F748B"),
                    FontSize = 11
                }
            }
        };

        content.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#213247"), Margin = new Thickness(0, 12, 0, 6) });
        content.Children.Add(footer);
        panel.RowDefinitions.Clear();
        panel.Children.Add(new ScrollView { Content = content });
        return panel;
    }

    private Button CreateDrawerButton(Func<string> textFactory, Func<Task> action)
    {
        var button = new Button
        {
            Text = textFactory(),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Fill,
            HeightRequest = 48,
            CornerRadius = 14,
            Padding = new Thickness(14, 0),
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#152233")
        };
        _textRefreshers.Add(() => button.Text = textFactory());
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private Button CreateDrawerIconButton(string text, Func<Task> action)
    {
        var button = new Button
        {
            Text = text,
            WidthRequest = 42,
            HeightRequest = 42,
            CornerRadius = 18,
            Padding = 0,
            FontSize = 16,
            BackgroundColor = Color.FromArgb("#172538"),
            TextColor = Colors.White
        };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private View CreateLanguageSelector()
    {
        return _languagePicker;
    }

    private async Task OpenDrawerAsync()
    {
        if (_drawerOpen || _drawerPanel is null || _drawerShade is null)
            return;
        _drawerOpen = true;
        _drawerShade.Opacity = 0;
        _drawerShade.IsVisible = true;
        _drawerPanel.TranslationX = -330;
        _drawerPanel.IsVisible = true;
        await Task.WhenAll(
            _drawerShade.FadeTo(0.62, 160, Easing.CubicOut),
            _drawerPanel.TranslateTo(0, 0, 220, Easing.CubicOut));
    }

    private async Task CloseDrawerAsync()
    {
        if (!_drawerOpen || _drawerPanel is null || _drawerShade is null)
            return;
        _drawerOpen = false;
        await Task.WhenAll(
            _drawerShade.FadeTo(0, 140, Easing.CubicIn),
            _drawerPanel.TranslateTo(-330, 0, 180, Easing.CubicIn));
        _drawerShade.IsVisible = false;
        _drawerPanel.IsVisible = false;
    }

    private async void OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_updatingLanguagePicker)
            return;

        var newLanguage = _languagePicker.SelectedIndex == 1 ? AppLanguage.En : AppLanguage.PtBr;
        await SetLanguageAsync(newLanguage);
    }

    private async Task SetLanguageAsync(AppLanguage language)
    {
        if (_language == language)
            return;

        _language = language;
        Preferences.Set(LanguagePreferenceKey, _language == AppLanguage.En ? "en" : "pt-BR");
        _updatingLanguagePicker = true;
        _languagePicker.SelectedIndex = _language == AppLanguage.En ? 1 : 0;
        _updatingLanguagePicker = false;

        RebuildTexts();
        await RefreshBrowseItemsAsync();
        if (_selectedItem is not null)
            UpdateSelectedText();
        if (_lastOpportunity is not null)
            UpdateOpportunity(_lastOpportunity);
        UpdateSearchModeVisibility();
        _cacheInfoLabel.Text = GetCacheInfoText();
        _serverTimeHeaderLabel.Text = GetServerTimeText();
        await RefreshGoldAsync();
        _statusLabel.Text = _language == AppLanguage.PtBr ? "Idioma alterado para português." : "Language changed to English.";
    }

    private async void OnServerChanged(object? sender, EventArgs e)
    {
        if (_updatingServerPicker)
            return;

        Preferences.Set(ServerPreferenceKey, GetSelectedServerKey());
        await RefreshGoldAsync();
        if (_selectedItem is not null)
            await SearchPricesAsync();
    }

    private void RebuildTexts()
    {
        var quickTier = _quickTierPicker.SelectedIndex;
        var quickEnchant = _quickEnchantPicker.SelectedIndex;
        var quickQuality = _quickQualityPicker.SelectedIndex;
        var tier = _tierPicker.SelectedIndex;
        var enchant = _enchantPicker.SelectedIndex;
        var quality = _qualityPicker.SelectedIndex;
        var catIndex = _categoryPicker.SelectedIndex;
        var groupIndex = _groupPicker.SelectedIndex;
        var craftTier = _craftingTierPicker.SelectedIndex;
        var craftEnchant = _craftingEnchantPicker.SelectedIndex;
        var craftQuality = _craftingQualityPicker.SelectedIndex;

        _updatingLanguagePicker = true;
        _languagePicker.SelectedIndex = _language == AppLanguage.En ? 1 : 0;
        _updatingLanguagePicker = false;
        foreach (var refresh in _textRefreshers)
            refresh();

        _quickSearchEntry.Placeholder = T("Digite parte do nome. Ex: bolsa, espada, capa...", "Type part of the name. Example: bag, sword, cape...");
        _craftingSearchEntry.Placeholder = T("Digite o item para craftar. Ex: espada, bolsa, capacete...", "Type the item to craft. Example: sword, bag, helmet...");
        _languagePicker.Title = T("Idioma", "Language");
        _serverPicker.Title = T("Servidor", "Server");
        _refiningResourcePicker.Title = T("Material bruto", "Raw material");
        _refiningTierPicker.Title = "Tier";
        _refiningEnchantPicker.Title = T("Encant.", "Enchant");
        _craftingTierPicker.Title = "Tier";
        _craftingEnchantPicker.Title = T("Encant.", "Enchant");
        _craftingQualityPicker.Title = T("Qualidade", "Quality");
        _categoryPicker.Title = T("Categoria", "Category");
        _groupPicker.Title = T("Grupo", "Group");
        _quickTierPicker.Title = "Tier";
        _tierPicker.Title = "Tier";
        _quickEnchantPicker.Title = T("Encant.", "Enchant");
        _enchantPicker.Title = T("Encant.", "Enchant");
        _quickQualityPicker.Title = T("Qualidade", "Quality");
        _qualityPicker.Title = T("Qualidade", "Quality");
        _quickSearchButton.Text = T("Buscar preços", "Fetch prices");
        _refiningButton.Text = T("Refinar", "Refine");
        _craftingButton.Text = T("Calcular craft", "Calculate craft");
        _refiningIntroLabel.Text = T("Escolha o material, tier e encantamento para simular compra dos materiais necessários, refino e venda do refinado.", "Choose material, tier and enchantment to simulate buying needed materials, refining and selling the refined resource.");
        _craftingIntroLabel.Text = T("Pesquise o item, selecione tier, encantamento e qualidade. O app calcula ingredientes baratos, venda do item pronto e lucro líquido.", "Search the item, choose tier, enchantment and quality. The app calculates cheapest ingredients, finished item sale and net profit.");
        PopulateRefiningResourcePicker();
        _serverTimeHeaderLabel.Text = GetServerTimeText();
        _quickModeButton.Text = T("⚡ Busca rápida", "⚡ Quick search");
        _categoryModeButton.Text = T("🛒 Por categoria", "🛒 By category");

        PopulateQualityPicker(_quickQualityPicker, quickQuality);
        PopulateQualityPicker(_qualityPicker, quality);
        PopulateQualityPicker(_craftingQualityPicker, craftQuality);
        _quickTierPicker.SelectedIndex = quickTier;
        _quickEnchantPicker.SelectedIndex = quickEnchant;
        _tierPicker.SelectedIndex = tier;
        _enchantPicker.SelectedIndex = enchant;
        _craftingTierPicker.SelectedIndex = craftTier;
        _craftingEnchantPicker.SelectedIndex = craftEnchant;

        PopulateCategoryPicker(catIndex, groupIndex);
        UpdateSearchModeVisibility();
        UpdateMainScreenVisibility();
        _drawerSectionTitle!.Text = T("Toque em Favoritos ou Histórico", "Tap Favorites or History");
        UpdateCaerleonButtons();
    }

    private async Task ReloadCategoriesAsync()
    {
        Preferences.Set(BaseUrlPreferenceKey, GetBaseUrl());
        _reloadCategoriesButton.IsEnabled = false;
        _reloadCategoriesButton.Text = T("Carregando...", "Loading...");
        _statusLabel.Text = T("Carregando categorias do catálogo...", "Loading catalog categories...");

        try
        {
            var categories = await _apiClient.GetCategoriesAsync(GetBaseUrl());
            _categories.Clear();
            _categories.AddRange(categories);
            PopulateCategoryPicker();
            _categoriesLoaded = true;
            _statusLabel.Text = T($"Categorias carregadas: {_categories.Count}.", $"Categories loaded: {_categories.Count}.");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = T($"Erro ao carregar categorias: {ex.Message}", $"Error loading categories: {ex.Message}");
        }
        finally
        {
            _reloadCategoriesButton.IsEnabled = true;
            _reloadCategoriesButton.Text = T("Atualizar catálogo", "Refresh catalog");
        }
    }

    private void PopulateCategoryPicker(int selectedCategoryIndex = 0, int selectedGroupIndex = 0)
    {
        _updatingCatalogPickers = true;
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add(T("Escolha uma categoria", "Choose a category"));
        foreach (var category in _categories)
            _categoryPicker.Items.Add(UiText.CategoryName(category, _language));
        _categoryPicker.SelectedIndex = Math.Clamp(selectedCategoryIndex, 0, Math.Max(0, _categoryPicker.Items.Count - 1));
        PopulateGroupPicker(selectedGroupIndex);
        _updatingCatalogPickers = false;
    }

    private void PopulateGroupPicker(int selectedGroupIndex = 0)
    {
        _currentGroups.Clear();
        _groupPicker.Items.Clear();
        _groupPicker.Items.Add(T("Escolha um grupo", "Choose a group"));
        var category = GetSelectedCategory();
        if (category is not null)
        {
            _currentGroups.AddRange(category.Groups);
            foreach (var group in _currentGroups)
                _groupPicker.Items.Add(UiText.GroupName(group, _language));
        }
        _groupPicker.SelectedIndex = Math.Clamp(selectedGroupIndex, 0, Math.Max(0, _groupPicker.Items.Count - 1));
    }

    private async void OnQuickSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingQuickText)
            return;
        if (_selectionMode == SelectionMode.QuickSearch)
            _selectedItem = null;
        await SearchQuickSuggestionsAsync(e.NewTextValue ?? string.Empty);
    }

    private async Task SearchQuickSuggestionsAsync(string query)
    {
        query = query.Trim();
        var currentVersion = ++_quickSearchVersion;
        if (query.Length < 2)
        {
            _quickSuggestionsLayout.Children.Clear();
            _quickSuggestionsLayout.IsVisible = false;
            return;
        }

        await Task.Delay(180);
        if (currentVersion != _quickSearchVersion)
            return;

        try
        {
            var apiQuery = UiText.SearchQueryForApi(query, _language);
            var items = await _apiClient.SearchItemsAsync(GetBaseUrl(), apiQuery, GetQuickTierText(), GetQuickEnchant());
            if (currentVersion != _quickSearchVersion)
                return;

            _quickSuggestionsLayout.Children.Clear();
            _visibleItems.Clear();
            var visibleQuickItems = items.Take(12).ToList();
            _visibleItems.AddRange(visibleQuickItems);
            foreach (var item in visibleQuickItems)
                _quickSuggestionsLayout.Children.Add(CreateItemButton(item, SelectionMode.QuickSearch));
            _quickSuggestionsLayout.IsVisible = _quickSuggestionsLayout.Children.Count > 0;
            _statusLabel.Text = _quickSuggestionsLayout.Children.Count > 0
                ? T($"{_quickSuggestionsLayout.Children.Count} sugestão(ões).", $"{_quickSuggestionsLayout.Children.Count} suggestion(s).")
                : T("Nenhum item encontrado na busca rápida.", "No item found in quick search.");
        }
        catch (Exception ex)
        {
            _quickSuggestionsLayout.Children.Clear();
            _quickSuggestionsLayout.IsVisible = false;
            _statusLabel.Text = T($"Erro na busca rápida: {ex.Message}", $"Quick search error: {ex.Message}");
        }
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_updatingCatalogPickers)
            return;
        _updatingCatalogPickers = true;
        PopulateGroupPicker();
        _updatingCatalogPickers = false;
        await RefreshBrowseItemsAsync();
    }

    private async void OnGroupChanged(object? sender, EventArgs e)
    {
        if (_updatingCatalogPickers)
            return;
        await RefreshBrowseItemsAsync();
    }

    private async void OnQuickFilterChanged(object? sender, EventArgs e)
    {
        if (_selectionMode == SelectionMode.QuickSearch && _selectedItem is not null)
        {
            await RefreshSelectedItemForCurrentFiltersAsync();
            await SearchPricesAsync();
        }
        if (!string.IsNullOrWhiteSpace(_quickSearchEntry.Text))
            await SearchQuickSuggestionsAsync(_quickSearchEntry.Text);
    }

    private async void OnCategoryFilterChanged(object? sender, EventArgs e)
    {
        if (_selectionMode == SelectionMode.Category && _selectedItem is not null)
        {
            await RefreshSelectedItemForCurrentFiltersAsync();
            await SearchPricesAsync();
        }
        await RefreshBrowseItemsAsync();
    }

    private async Task RefreshBrowseItemsAsync()
    {
        var currentVersion = ++_browseVersion;
        var category = GetSelectedCategory();
        var group = GetSelectedGroup();

        _browseItemsLayout.Children.Clear();
        _browseItemsLayout.IsVisible = false;
        _browseToggleButton.IsVisible = false;
        _browseItemsExpanded = false;
        _browseItemsCount = 0;

        if (category is null)
        {
            _browseTitleLabel.Text = T("Escolha uma categoria e depois um grupo.", "Choose a category and then a group.");
            return;
        }
        if (group is null)
        {
            _browseTitleLabel.Text = T($"Categoria: {CategoryName(category)}. Agora selecione um grupo para ver os itens.", $"Category: {CategoryName(category)}. Now choose a group to see items.");
            return;
        }

        _browseTitleLabel.Text = T($"Selecione o item — {CategoryName(category)} › {GroupName(group)}.", $"Select an item — {CategoryName(category)} › {GroupName(group)}.");

        try
        {
            var items = await _apiClient.SearchItemsAsync(GetBaseUrl(), string.Empty, GetCategoryTierText(), GetCategoryEnchant(), category.Key, group.Key);
            if (currentVersion != _browseVersion)
                return;

            var visibleItems = items.Take(45).ToList();
            _visibleItems.Clear();
            _visibleItems.AddRange(visibleItems);
            foreach (var item in visibleItems)
                _browseItemsLayout.Children.Add(CreateItemButton(item, SelectionMode.Category));
            _browseItemsCount = visibleItems.Count;
            _browseToggleButton.IsVisible = _browseItemsCount > 0;
            _browseToggleButton.Text = items.Count > 45
                ? T($"▶ Selecione o item ({_browseItemsCount} de {items.Count})", $"▶ Select item ({_browseItemsCount} of {items.Count})")
                : T($"▶ Selecione o item ({_browseItemsCount})", $"▶ Select item ({_browseItemsCount})");
            _browseTitleLabel.Text = _browseItemsCount > 0
                ? T($"Selecione o item — {CategoryName(category)} › {GroupName(group)}.", $"Select an item — {CategoryName(category)} › {GroupName(group)}.")
                : T($"Nenhum item encontrado em {CategoryName(category)} › {GroupName(group)}.", $"No item found in {CategoryName(category)} › {GroupName(group)}.");
        }
        catch (Exception ex)
        {
            _browseTitleLabel.Text = T($"Erro ao carregar itens: {ex.Message}", $"Error loading items: {ex.Message}");
        }
    }

    private View CreateItemButton(ItemSuggestion item, SelectionMode source)
    {
        var itemName = UiText.ItemName(item, _language);
        var quality = source == SelectionMode.QuickSearch ? GetQuickQuality() : GetCategoryQuality();
        var subtitle = source == SelectionMode.QuickSearch
            ? T($"Busca rápida • {GetQuickTierText()}.{GetQuickEnchant()} • {UiText.QualityName(quality, _language)}", $"Quick search • {GetQuickTierText()}.{GetQuickEnchant()} • {UiText.QualityName(quality, _language)}")
            : T($"{GetCategoryTierText()}.{GetCategoryEnchant()} • {UiText.QualityName(quality, _language)}", $"{GetCategoryTierText()}.{GetCategoryEnchant()} • {UiText.QualityName(quality, _language)}");

        var image = new Image
        {
            Source = ItemIconCacheService.GetBestSource(item.ItemId, quality, 48),
            WidthRequest = 48,
            HeightRequest = 48,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#0B1420")
        };

        _ = ItemIconCacheService.LoadIntoAsync(image, item.ItemId, quality, 48);

        var heart = new Button
        {
            Text = IsFavorite(item, source) ? "♥" : "♡",
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 16,
            Padding = 0,
            FontSize = 20,
            BackgroundColor = Color.FromArgb("#202B3D"),
            TextColor = Color.FromArgb("#FF6B96")
        };
        heart.Clicked += (_, _) =>
        {
            ToggleFavoriteForItem(item, source);
            heart.Text = IsFavorite(item, source) ? "♥" : "♡";
        };

        var textBlock = new VerticalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.FillAndExpand,
            Children =
            {
                new Label
                {
                    Text = itemName,
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 14,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = subtitle,
                    TextColor = Color.FromArgb("#8FA2B8"),
                    FontSize = 12
                }
            }
        };

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        Grid.SetColumn(image, 0);
        Grid.SetColumn(textBlock, 1);
        Grid.SetColumn(heart, 2);
        content.Children.Add(image);
        content.Children.Add(textBlock);
        content.Children.Add(heart);

        var card = CreateCard(content, Color.FromArgb("#263B55"), Color.FromArgb("#162437"));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await SelectSuggestionAsync(item, source);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private void ToggleBrowseItems()
    {
        if (_browseItemsCount <= 0)
            return;
        _browseItemsExpanded = !_browseItemsExpanded;
        _browseItemsLayout.IsVisible = _browseItemsExpanded;
        _browseToggleButton.Text = _browseItemsExpanded
            ? T($"▼ Ocultar itens ({_browseItemsCount})", $"▼ Hide items ({_browseItemsCount})")
            : T($"▶ Selecione o item ({_browseItemsCount})", $"▶ Select item ({_browseItemsCount})");
    }

    private async Task SelectSuggestionAsync(ItemSuggestion selected, SelectionMode source)
    {
        _selectedItem = selected;
        _selectionMode = source;
        UpdateSelectedText();
        UpdateSelectedItemIcon(GetItemToQuery());
        UpdateFavoriteButton();

        if (source == SelectionMode.QuickSearch)
        {
            _updatingQuickText = true;
            _quickSearchEntry.Text = UiText.ItemName(selected, _language);
            _updatingQuickText = false;
            _quickSuggestionsLayout.Children.Clear();
            _quickSuggestionsLayout.IsVisible = false;
        }
        else
        {
            _browseItemsExpanded = false;
            _browseItemsLayout.IsVisible = false;
            if (_browseItemsCount > 0)
                _browseToggleButton.Text = T($"▶ Trocar item ({_browseItemsCount})", $"▶ Change item ({_browseItemsCount})");
        }

        _statusLabel.Text = T($"Selecionado: {UiText.ItemName(selected, _language)}. Buscando preços...", $"Selected: {UiText.ItemName(selected, _language)}. Fetching prices...");
        await SearchPricesAsync();
    }


    private View CreateCraftingItemButton(ItemSuggestion item)
    {
        var quality = GetCraftingQuality();
        var image = new Image
        {
            Source = ItemIconCacheService.GetBestSource(item.ItemId, quality, 44),
            WidthRequest = 44,
            HeightRequest = 44,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#0B1420")
        };
        _ = ItemIconCacheService.LoadIntoAsync(image, item.ItemId, quality, 44);

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        Grid.SetColumn(image, 0);
        var label = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = UiText.ItemName(item, _language), TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap },
                new Label { Text = T($"Craft • {GetCraftingTierText()}.{GetCraftingEnchant()} • {UiText.QualityName(quality, _language)}", $"Craft • {GetCraftingTierText()}.{GetCraftingEnchant()} • {UiText.QualityName(quality, _language)}"), TextColor = Color.FromArgb("#8FA2B8"), FontSize = 12 }
            }
        };
        Grid.SetColumn(label, 1);
        content.Children.Add(image);
        content.Children.Add(label);

        var card = CreateCard(content, Color.FromArgb("#313D69"), Color.FromArgb("#162035"));
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await SelectCraftingItemAsync(item);
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private async Task SelectCraftingItemAsync(ItemSuggestion item)
    {
        _selectedCraftItem = item;
        _updatingCraftingText = true;
        _craftingSearchEntry.Text = UiText.ItemName(item, _language);
        _updatingCraftingText = false;
        _craftingSuggestionsLayout.Children.Clear();
        _craftingSuggestionsLayout.IsVisible = false;
        _statusLabel.Text = T($"Item de craft selecionado: {UiText.ItemName(item, _language)}.", $"Craft item selected: {UiText.ItemName(item, _language)}.");
        await SearchCraftingAsync();
    }

    private async Task SearchPricesAsync()
    {
        var itemId = GetItemToQuery();
        if (string.IsNullOrWhiteSpace(itemId))
        {
            _statusLabel.Text = T("Selecione um item primeiro.", "Select an item first.");
            return;
        }

        Preferences.Set(BaseUrlPreferenceKey, GetBaseUrl());
        _quickSearchButton.IsEnabled = false;
        _quickSearchButton.Text = T("Buscando...", "Fetching...");
        _statusLabel.Text = T("Consultando preços com cache de 15 minutos...", "Fetching prices with 15-minute cache...");

        try
        {
            var tier = GetActiveTierNumber();
            var enchant = GetActiveEnchant();
            var quality = GetActiveQuality();
            var baseUrl = GetBaseUrl();
            UpdateSelectedItemIcon(itemId);

            var server = GetSelectedServerKey();
            var pricesTask = _apiClient.GetPricesAsync(baseUrl, itemId, tier, enchant, quality, server);
            var bestTask = _apiClient.GetBestCityAsync(baseUrl, itemId, tier, enchant, quality, server);
            await Task.WhenAll(pricesTask, bestTask);

            _lastOpportunity = bestTask.Result;
            UpdateOpportunity(_lastOpportunity);
            UpdatePrices(pricesTask.Result, _lastOpportunity);
            AddCurrentItemToHistory();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = T($"Erro ao buscar preços: {ex.Message}", $"Error fetching prices: {ex.Message}");
        }
        finally
        {
            _quickSearchButton.IsEnabled = true;
            _quickSearchButton.Text = T("Buscar preços", "Fetch prices");
        }
    }

    private void UpdateOpportunity(MarketOpportunity? opportunity)
    {
        if (opportunity is null)
        {
            _opportunityTitle.Text = T("Melhor oportunidade", "Best opportunity");
            _opportunityRoute.Text = T("Nenhuma resposta de oportunidade.", "No opportunity response.");
            _opportunityProfit.Text = T("Lucro: -", "Profit: -");
            _opportunityProfit.TextColor = Color.FromArgb("#8FA2B8");
            return;
        }

        _opportunityTitle.Text = UiText.ItemName(opportunity, _language);

        if (opportunity.BestBuyPrice > 0 && opportunity.BestSellPrice > 0 && !SameCity(opportunity.BestBuyCity, opportunity.BestSellCity))
        {
            _opportunityRoute.Text = T(
                $"Comprar: {opportunity.BestBuyCity} • {opportunity.BestBuyPrice:N0}\nVender: {opportunity.BestSellCity} • {opportunity.BestSellPrice:N0}",
                $"Buy: {opportunity.BestBuyCity} • {opportunity.BestBuyPrice:N0}\nSell: {opportunity.BestSellCity} • {opportunity.BestSellPrice:N0}");

            _opportunityProfit.Text = T(
                $"Com premium: {FormatProfit(opportunity.ProfitWithPremium)}\nSem premium: {FormatProfit(opportunity.ProfitWithoutPremium)}",
                $"With premium: {FormatProfit(opportunity.ProfitWithPremium)}\nNo premium: {FormatProfit(opportunity.ProfitWithoutPremium)}");
            _opportunityProfit.TextColor = opportunity.ProfitWithPremium > 0 ? Color.FromArgb("#4FE18A") : Color.FromArgb("#FFB86B");
            return;
        }

        if (opportunity.BestBuyPrice > 0)
        {
            _opportunityRoute.Text = T(
                $"Menor preço encontrado: {opportunity.BestBuyCity} por {opportunity.BestBuyPrice:N0}",
                $"Lowest price found: {opportunity.BestBuyCity} for {opportunity.BestBuyPrice:N0}");
        }
        else
        {
            _opportunityRoute.Text = string.IsNullOrWhiteSpace(opportunity.Message)
                ? T("Nenhuma rota com lucro positivo entre cidades diferentes.", "No positive-profit route between different cities.")
                : opportunity.Message;
        }

        _opportunityProfit.Text = T("Sem oportunidade válida", "No valid opportunity");
        _opportunityProfit.TextColor = Color.FromArgb("#FFB86B");
    }

    private static string FormatProfit(long value) => value >= 0 ? $"+{value:N0}" : value.ToString("N0");

    private static string FormatProfitPercent(decimal value) => value >= 0 ? $"+{value:0.##}%" : $"{value:0.##}%";

    private void UpdatePrices(IEnumerable<MarketPrice> prices, MarketOpportunity? opportunity)
    {
        _pricesLayout.Children.Clear();
        var hasBestMarkers = opportunity is not null
            && opportunity.BestBuyPrice > 0
            && opportunity.BestSellPrice > 0
            && !SameCity(opportunity.BestBuyCity, opportunity.BestSellCity)
            && !string.Equals(opportunity.BestBuyCity, "N/A", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(opportunity.BestSellCity, "N/A", StringComparison.OrdinalIgnoreCase);
        var bestBuyCity = hasBestMarkers ? opportunity!.BestBuyCity : string.Empty;
        var bestSellCity = hasBestMarkers ? opportunity!.BestSellCity : string.Empty;

        var ordered = prices
            .OrderBy(x => hasBestMarkers && SameCity(x.City, bestBuyCity) ? 0 : hasBestMarkers && SameCity(x.City, bestSellCity) ? 1 : 2)
            .ThenBy(x => x.SellPriceMin <= 0)
            .ThenBy(x => x.SellPriceMin)
            .ToList();

        foreach (var price in ordered)
        {
            var isBestBuy = hasBestMarkers && SameCity(price.City, bestBuyCity);
            var isBestSell = hasBestMarkers && SameCity(price.City, bestSellCity);
            _pricesLayout.Children.Add(CreatePriceCard(price, isBestBuy, isBestSell));
        }

        _cacheInfoLabel.Text = GetCacheInfoText();
        _statusLabel.Text = ordered.Count > 0
            ? T("Preços carregados.", "Prices loaded.")
            : T("A API respondeu, mas não trouxe preço válido.", "The API responded, but no valid price was returned.");
    }

    private View CreatePriceCard(MarketPrice price, bool isBestBuy, bool isBestSell)
    {
        var tag = isBestBuy ? T("MELHOR COMPRA", "BEST BUY") : isBestSell ? T("MELHOR VENDA", "BEST SELL") : string.Empty;
        var border = isBestBuy ? Color.FromArgb("#4FE18A") : isBestSell ? Color.FromArgb("#8AB4FF") : Color.FromArgb("#25364A");
        var background = isBestBuy ? Color.FromArgb("#103320") : isBestSell ? Color.FromArgb("#142642") : Color.FromArgb("#101B28");

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var cityLabel = new Label
        {
            Text = price.City,
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        Grid.SetColumn(cityLabel, 0);
        header.Children.Add(cityLabel);

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var tagLabel = new Label
            {
                Text = tag,
                TextColor = isBestBuy ? Color.FromArgb("#4FE18A") : Color.FromArgb("#8AB4FF"),
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                VerticalTextAlignment = TextAlignment.Center
            };
            Grid.SetColumn(tagLabel, 1);
            header.Children.Add(tagLabel);
        }

        var content = new VerticalStackLayout
        {
            Spacing = 7,
            Children =
            {
                header,
                CreatePriceRow(T("Comprar", "Buy"), price.SellPriceMinText, Color.FromArgb("#C8D5E3")),
                CreatePriceRow(T("Venda imediata", "Instant sell"), price.BuyPriceMaxText, Color.FromArgb("#8FA2B8")),
                new Label
                {
                    Text = T($"Atualizado às {price.UpdatedAtText} UTC", $"Updated at {price.UpdatedAtText} UTC"),
                    TextColor = Color.FromArgb("#6F8399"),
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.End
                }
            }
        };

        return CreateCard(content, border, background);
    }

    private View CreatePriceRow(string label, string value, Color valueColor)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var left = new Label
        {
            Text = label,
            TextColor = Color.FromArgb("#8FA2B8"),
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation
        };

        var right = new Label
        {
            Text = value,
            TextColor = valueColor,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End
        };

        Grid.SetColumn(left, 0);
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }

    private async Task RefreshSelectedItemForCurrentFiltersAsync()
    {
        if (_selectedItem is null)
            return;
        var selected = _selectedItem;
        var familyKey = GetItemFamilyKey(selected.ItemId);
        var tier = GetActiveTierNumber();
        var enchant = GetActiveEnchant();
        var tierText = $"T{tier}";

        var fallback = new ItemSuggestion
        {
            ItemId = ApplyTierAndEnchant(selected.ItemId, tier, enchant),
            Name = selected.DisplayName,
            Tier = tier,
            Enchantment = enchant,
            Category = selected.Category,
            SubCategory = selected.SubCategory
        };

        try
        {
            var candidates = await _apiClient.SearchItemsAsync(GetBaseUrl(), selected.DisplayName, tierText, enchant, selected.Category, selected.SubCategory);
            var matched = candidates.FirstOrDefault(x => SameFamily(x.ItemId, familyKey));
            _selectedItem = matched ?? fallback;
        }
        catch
        {
            _selectedItem = fallback;
        }
        UpdateSelectedText();
        UpdateSelectedItemIcon(GetItemToQuery());
        UpdateFavoriteButton();
    }

    private void UpdateSelectedText()
    {
        if (_selectedItem is null)
        {
            _selectedItemLabel.Text = T("Nenhum item selecionado", "No item selected");
            return;
        }
        _selectedItemLabel.Text = $"{UiText.ItemName(_selectedItem, _language)} • T{GetActiveTierNumber()}.{GetActiveEnchant()} • {UiText.QualityName(GetActiveQuality(), _language)}";
    }

    private string GetItemToQuery()
    {
        if (_selectedItem is null)
            return string.Empty;
        return ApplyTierAndEnchant(_selectedItem.ItemId, GetActiveTierNumber(), GetActiveEnchant());
    }

    private void UpdateSelectedItemIcon(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return;
        var quality = GetActiveQuality();
        _selectedItemIcon.Source = ItemIconCacheService.GetBestSource(itemId, quality, 96);
        _ = ItemIconCacheService.LoadIntoAsync(_selectedItemIcon, itemId, quality, 96);
    }

    private Task ShowSavedItemsInDrawerAsync(string key, string title)
    {
        if (_drawerListLayout is null || _drawerSectionTitle is null)
            return Task.CompletedTask;

        _drawerSectionTitle.Text = title;
        _drawerListLayout.Children.Clear();
        var items = LoadSavedItems(key);
        if (items.Count == 0)
        {
            _drawerListLayout.Children.Add(new Label
            {
                Text = T("Nenhum item salvo ainda.", "No saved items yet."),
                TextColor = Color.FromArgb("#8FA2B8"),
                FontSize = 13
            });
            return Task.CompletedTask;
        }

        foreach (var saved in items.Take(24))
            _drawerListLayout.Children.Add(CreateSavedDrawerItem(saved));

        return Task.CompletedTask;
    }

    private View CreateSavedDrawerItem(SavedMarketItem saved)
    {
        var name = _language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(saved.NamePtBr)
            ? saved.NamePtBr
            : UiText.TranslateItemNamePublic(saved.Name, saved.ItemId, _language);
        var button = new Button
        {
            Text = $"{name}\nT{saved.Tier}.{saved.Enchantment} • {UiText.QualityName(saved.Quality, _language)}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap,
            BackgroundColor = Color.FromArgb("#152233"),
            TextColor = Colors.White,
            CornerRadius = 14,
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 56
        };
        button.Clicked += async (_, _) =>
        {
            _selectedItem = new ItemSuggestion
            {
                ItemId = saved.ItemId,
                Name = saved.Name,
                NamePtBr = saved.NamePtBr,
                Tier = saved.Tier,
                Enchantment = saved.Enchantment,
                Category = saved.Category,
                SubCategory = saved.SubCategory
            };
            _selectionMode = saved.Source == "quick" ? SelectionMode.QuickSearch : SelectionMode.Category;
            _activeInputMode = _selectionMode;
            SetActiveFilters(saved.Tier, saved.Enchantment, saved.Quality);
            UpdateSearchModeVisibility();
            UpdateSelectedText();
            UpdateSelectedItemIcon(GetItemToQuery());
            UpdateFavoriteButton();
            await CloseDrawerAsync();
            await SearchPricesAsync();
        };
        return button;
    }

    private void ToggleFavoriteForSelectedItem()
    {
        if (_selectedItem is null)
        {
            _statusLabel.Text = T("Selecione um item antes de favoritar.", "Select an item before favoriting.");
            return;
        }

        var favorites = LoadSavedItems(FavoritesPreferenceKey);
        var key = SavedKey(_selectedItem.ItemId, GetActiveTierNumber(), GetActiveEnchant(), GetActiveQuality());
        var existing = favorites.FindIndex(x => SavedKey(x.ItemId, x.Tier, x.Enchantment, x.Quality) == key);
        if (existing >= 0)
        {
            favorites.RemoveAt(existing);
            _statusLabel.Text = T("Removido dos favoritos.", "Removed from favorites.");
        }
        else
        {
            favorites.Insert(0, CreateSavedItem());
            _statusLabel.Text = T("Adicionado aos favoritos.", "Added to favorites.");
        }
        SaveSavedItems(FavoritesPreferenceKey, favorites.Take(80).ToList());
        UpdateFavoriteButton();
    }

    private void ToggleFavoriteForItem(ItemSuggestion item, SelectionMode source)
    {
        var tier = source == SelectionMode.QuickSearch ? GetQuickTierNumber() : GetCategoryTierNumber();
        var enchant = source == SelectionMode.QuickSearch ? GetQuickEnchant() : GetCategoryEnchant();
        var quality = source == SelectionMode.QuickSearch ? GetQuickQuality() : GetCategoryQuality();
        var itemId = ApplyTierAndEnchant(item.ItemId, tier, enchant);
        var favorites = LoadSavedItems(FavoritesPreferenceKey);
        var key = SavedKey(itemId, tier, enchant, quality);
        var existing = favorites.FindIndex(x => SavedKey(x.ItemId, x.Tier, x.Enchantment, x.Quality) == key);

        if (existing >= 0)
        {
            favorites.RemoveAt(existing);
            _statusLabel.Text = T("Removido dos favoritos.", "Removed from favorites.");
        }
        else
        {
            favorites.Insert(0, new SavedMarketItem
            {
                ItemId = itemId,
                Name = item.DisplayName,
                NamePtBr = item.NamePtBr,
                Tier = tier,
                Enchantment = enchant,
                Quality = quality,
                Category = item.Category,
                SubCategory = item.SubCategory,
                Source = source == SelectionMode.QuickSearch ? "quick" : "category",
                SavedAt = DateTimeOffset.UtcNow
            });
            _statusLabel.Text = T("Adicionado aos favoritos.", "Added to favorites.");
        }

        SaveSavedItems(FavoritesPreferenceKey, favorites.Take(80).ToList());
        UpdateFavoriteButton();
    }

    private bool IsFavorite(ItemSuggestion item, SelectionMode source)
    {
        var tier = source == SelectionMode.QuickSearch ? GetQuickTierNumber() : GetCategoryTierNumber();
        var enchant = source == SelectionMode.QuickSearch ? GetQuickEnchant() : GetCategoryEnchant();
        var quality = source == SelectionMode.QuickSearch ? GetQuickQuality() : GetCategoryQuality();
        var itemId = ApplyTierAndEnchant(item.ItemId, tier, enchant);
        var key = SavedKey(itemId, tier, enchant, quality);
        return LoadSavedItems(FavoritesPreferenceKey).Any(x => SavedKey(x.ItemId, x.Tier, x.Enchantment, x.Quality) == key);
    }

    private void AddCurrentItemToHistory()
    {
        if (_selectedItem is null)
            return;
        var history = LoadSavedItems(HistoryPreferenceKey);
        var item = CreateSavedItem();
        var key = SavedKey(item.ItemId, item.Tier, item.Enchantment, item.Quality);
        history.RemoveAll(x => SavedKey(x.ItemId, x.Tier, x.Enchantment, x.Quality) == key);
        history.Insert(0, item);
        SaveSavedItems(HistoryPreferenceKey, history.Take(50).ToList());
    }

    private SavedMarketItem CreateSavedItem()
    {
        var item = _selectedItem!;
        return new SavedMarketItem
        {
            ItemId = GetItemToQuery(),
            Name = item.DisplayName,
            NamePtBr = item.NamePtBr,
            Tier = GetActiveTierNumber(),
            Enchantment = GetActiveEnchant(),
            Quality = GetActiveQuality(),
            Category = item.Category,
            SubCategory = item.SubCategory,
            Source = _selectionMode == SelectionMode.QuickSearch ? "quick" : "category",
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    private void UpdateFavoriteButton()
    {
        if (_selectedItem is null)
        {
            _favoriteButton.Text = "♡";
            return;
        }
        var favorites = LoadSavedItems(FavoritesPreferenceKey);
        var key = SavedKey(GetItemToQuery(), GetActiveTierNumber(), GetActiveEnchant(), GetActiveQuality());
        _favoriteButton.Text = favorites.Any(x => SavedKey(x.ItemId, x.Tier, x.Enchantment, x.Quality) == key) ? "♥" : "♡";
    }

    private static List<SavedMarketItem> LoadSavedItems(string key)
    {
        try
        {
            return JsonSerializer.Deserialize<List<SavedMarketItem>>(Preferences.Get(key, "[]")) ?? new List<SavedMarketItem>();
        }
        catch
        {
            return new List<SavedMarketItem>();
        }
    }

    private static void SaveSavedItems(string key, List<SavedMarketItem> items)
    {
        Preferences.Set(key, JsonSerializer.Serialize(items));
    }

    private static string SavedKey(string itemId, int tier, int enchant, int quality) => $"{itemId.ToUpperInvariant()}|{tier}|{enchant}|{quality}";

    private CatalogCategory? GetSelectedCategory()
    {
        var index = _categoryPicker.SelectedIndex;
        return index > 0 && index - 1 < _categories.Count ? _categories[index - 1] : null;
    }

    private CatalogGroup? GetSelectedGroup()
    {
        var index = _groupPicker.SelectedIndex;
        return index > 0 && index - 1 < _currentGroups.Count ? _currentGroups[index - 1] : null;
    }

    private string CategoryName(CatalogCategory category) => UiText.CategoryName(category, _language).Replace($" ({category.Count})", string.Empty);
    private string GroupName(CatalogGroup group) => UiText.GroupName(group, _language).Replace($" ({group.Count})", string.Empty);

    private string GetSelectedServerKey()
    {
        return _serverPicker.SelectedIndex switch
        {
            1 => "east",
            2 => "europe",
            _ => "west"
        };
    }

    private static int ServerIndexFromKey(string? key)
    {
        return (key ?? "west").Trim().ToLowerInvariant() switch
        {
            "east" or "asia" => 1,
            "europe" or "eu" => 2,
            _ => 0
        };
    }

    private void SetActiveScreen(AppScreen screen)
    {
        _activeScreen = screen;
        UpdateMainScreenVisibility();
        _statusLabel.Text = screen switch
        {
            AppScreen.Refining => T("Refino aberto. Escolha o material e clique em Refinar.", "Refining opened. Choose the material and tap Refine."),
            AppScreen.Crafting => T("Craft aberto. Pesquise um item para calcular.", "Craft opened. Search an item to calculate."),
            _ => T("Mercado aberto.", "Market opened.")
        };
    }

    private void UpdateMainScreenVisibility()
    {
        if (_marketScreenContent is not null)
            _marketScreenContent.IsVisible = _activeScreen == AppScreen.Market;
        if (_refiningScreenContent is not null)
            _refiningScreenContent.IsVisible = _activeScreen == AppScreen.Refining;
        if (_craftingScreenContent is not null)
            _craftingScreenContent.IsVisible = _activeScreen == AppScreen.Crafting;
    }

    private void ToggleToolsOptions()
    {
        _toolsExpanded = !_toolsExpanded;
        if (_toolsOptionsLayout is not null)
            _toolsOptionsLayout.IsVisible = _toolsExpanded;
        RebuildTexts();
    }

    private void UpdateCaerleonButtons()
    {
        UpdateCaerleonButton(_refiningCaerleonButton, _includeCaerleonInRefining);
        UpdateCaerleonButton(_craftingCaerleonButton, _includeCaerleonInCrafting);
    }

    private void UpdateCaerleonButton(Button button, bool enabled)
    {
        button.Text = enabled
            ? T("Caerleon: ON", "Caerleon: ON")
            : T("Caerleon: OFF", "Caerleon: OFF");
        button.BackgroundColor = enabled ? Color.FromArgb("#6A2332") : Color.FromArgb("#152233");
        button.TextColor = enabled ? Color.FromArgb("#FFD37A") : Color.FromArgb("#DFEAF7");
    }

    private void PopulateRefiningResourcePicker()
    {
        var selected = Math.Max(0, _refiningResourcePicker.SelectedIndex);
        _refiningResourcePicker.Items.Clear();
        foreach (var item in GetRefiningResourceNames())
            _refiningResourcePicker.Items.Add(item);
        _refiningResourcePicker.SelectedIndex = Math.Clamp(selected, 0, Math.Max(0, _refiningResourcePicker.Items.Count - 1));
    }

    private string[] GetRefiningResourceNames()
    {
        return _language == AppLanguage.En
            ? new[] { "Logs", "Stone", "Ore", "Hide" }
            : new[] { "Troncos", "Pedras", "Minério de ferro", "Pelego" };
    }

    private string GetSelectedRefiningResourceKey()
    {
        return Math.Max(0, _refiningResourcePicker.SelectedIndex) switch
        {
            1 => "stone",
            2 => "ore",
            3 => "hide",
            _ => "wood"
        };
    }

    private int GetRefiningTierNumber() => ParseTier(_refiningTierPicker.SelectedItem?.ToString() ?? "T5");
    private int GetRefiningEnchant() => Math.Max(0, _refiningEnchantPicker.SelectedIndex);

    private async Task SearchRefiningAsync()
    {
        Preferences.Set(BaseUrlPreferenceKey, GetBaseUrl());
        _refiningButton.IsEnabled = false;
        _refiningButton.Text = T("Calculando...", "Calculating...");
        _refiningResultLayout.Children.Clear();
        _statusLabel.Text = T("Calculando oportunidade de refino com cache de 15 minutos...", "Calculating refining opportunity with 15-minute cache...");

        try
        {
            var quote = await _apiClient.GetRefiningQuoteAsync(
                GetBaseUrl(),
                GetSelectedRefiningResourceKey(),
                GetRefiningTierNumber(),
                GetRefiningEnchant(),
                GetSelectedServerKey(),
                _includeCaerleonInRefining);

            UpdateRefiningResult(quote);
        }
        catch (Exception ex)
        {
            _refiningResultLayout.Children.Add(CreateCard(new Label
            {
                Text = T($"Erro no refino: {ex.Message}", $"Refining error: {ex.Message}"),
                TextColor = Color.FromArgb("#FFB86B"),
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            }, Color.FromArgb("#7A3D32"), Color.FromArgb("#211416")));
            _statusLabel.Text = T("Erro ao calcular refino.", "Error calculating refining.");
        }
        finally
        {
            _refiningButton.IsEnabled = true;
            _refiningButton.Text = T("Refinar", "Refine");
        }
    }

    private void UpdateRefiningResult(RefiningQuote? quote)
    {
        _refiningResultLayout.Children.Clear();
        if (quote is null)
        {
            _refiningResultLayout.Children.Add(CreateCard(new Label
            {
                Text = T("Nenhuma resposta de refino.", "No refining response."),
                TextColor = Color.FromArgb("#FFB86B"),
                FontSize = 13
            }, Color.FromArgb("#7A3D32"), Color.FromArgb("#211416")));
            return;
        }

        var itemName = _language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(quote.RefinedItemNamePtBr)
            ? quote.RefinedItemNamePtBr
            : (_language == AppLanguage.PtBr ? UiText.TranslateItemNamePublic(quote.RefinedItemName, quote.RefinedItemId, _language) : quote.RefinedItemName);
        var hasProfit = quote.ProfitWithPremium > 0;
        var border = hasProfit ? Color.FromArgb("#4FE18A") : Color.FromArgb("#FFB86B");
        var background = hasProfit ? Color.FromArgb("#102A1D") : Color.FromArgb("#221B13");
        var title = hasProfit
            ? T("Refino com lucro", "Profitable refining")
            : T("Sem lucro no momento", "No profit right now");

        var summary = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = title, TextColor = border, FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = itemName, TextColor = Colors.White, FontSize = 15, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap },
                new Label
                {
                    Text = T(
                        $"Refinar em: {quote.RefiningCity} • retorno {quote.ResourceReturnRate * 100m:0.#}%\nCusto bruto: {quote.GrossIngredientCost:N0}\nCusto após bônus: {quote.TotalIngredientCost:N0}\nVenda: {quote.BestSellCity} • {quote.BestSellPrice:N0}\nTaxa premium: {quote.SaleTaxWithPremium:N0} • taxa sem premium: {quote.SaleTaxWithoutPremium:N0}\nCom premium: {FormatProfit(quote.ProfitWithPremium)} ({FormatProfitPercent(quote.ProfitPercentWithPremium)})\nSem premium: {FormatProfit(quote.ProfitWithoutPremium)} ({FormatProfitPercent(quote.ProfitPercentWithoutPremium)})",
                        $"Refine in: {quote.RefiningCity} • return {quote.ResourceReturnRate * 100m:0.#}%\nGross cost: {quote.GrossIngredientCost:N0}\nCost after bonus: {quote.TotalIngredientCost:N0}\nSell: {quote.BestSellCity} • {quote.BestSellPrice:N0}\nPremium tax: {quote.SaleTaxWithPremium:N0} • no-premium tax: {quote.SaleTaxWithoutPremium:N0}\nWith premium: {FormatProfit(quote.ProfitWithPremium)} ({FormatProfitPercent(quote.ProfitPercentWithPremium)})\nNo premium: {FormatProfit(quote.ProfitWithoutPremium)} ({FormatProfitPercent(quote.ProfitPercentWithoutPremium)})"),
                    TextColor = Color.FromArgb("#DCEBFF"),
                    FontSize = 13,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = T($"Atualizado às {quote.UpdatedAtText} UTC • válido até {quote.ValidUntilText} UTC", $"Updated at {quote.UpdatedAtText} UTC • valid until {quote.ValidUntilText} UTC"),
                    TextColor = Color.FromArgb("#8FA2B8"),
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
        _refiningResultLayout.Children.Add(CreateCard(summary, border, background));

        _refiningResultLayout.Children.Add(CreateLocalizedLabel(() => T("Comprar materiais", "Buy materials"), Colors.White, 17, FontAttributes.Bold));
        foreach (var ingredient in quote.Ingredients)
            _refiningResultLayout.Children.Add(CreateRefiningIngredientCard(ingredient));

        _refiningFooterLabel.Text = GetCacheInfoText();
        _statusLabel.Text = quote.HasOpportunityWithPremium
            ? T("Refino calculado com lucro líquido.", "Refining calculated with net profit.")
            : T("Refino calculado sem lucro líquido nos melhores preços atuais.", "Refining calculated without net profit at current best prices.");
    }

    private View CreateRefiningIngredientCard(RefiningIngredientCost ingredient)
    {
        var name = _language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(ingredient.ItemNamePtBr)
            ? ingredient.ItemNamePtBr
            : (_language == AppLanguage.PtBr ? UiText.TranslateItemNamePublic(ingredient.ItemName, ingredient.ItemId, _language) : ingredient.ItemName);

        var content = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = name,
                    TextColor = Colors.White,
                    FontAttributes = FontAttributes.Bold,
                    FontSize = 14,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = T(
                        $"{ingredient.Quantity}x em {ingredient.City} • unidade {ingredient.UnitPrice:N0} • custo ajustado {ingredient.TotalPrice:N0}",
                        $"{ingredient.Quantity}x in {ingredient.City} • unit {ingredient.UnitPrice:N0} • adjusted cost {ingredient.TotalPrice:N0}"),
                    TextColor = Color.FromArgb("#C8D5E3"),
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = T($"Atualizado às {ingredient.UpdatedAtText} UTC", $"Updated at {ingredient.UpdatedAtText} UTC"),
                    TextColor = Color.FromArgb("#6F8399"),
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.End
                }
            }
        };

        return CreateCard(content, Color.FromArgb("#25364A"), Color.FromArgb("#101B28"));
    }


    private async void OnCraftingSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updatingCraftingText)
            return;
        _selectedCraftItem = null;
        await SearchCraftingSuggestionsAsync(e.NewTextValue ?? string.Empty);
    }

    private async void OnCraftingFilterChanged(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_craftingSearchEntry.Text))
            await SearchCraftingSuggestionsAsync(_craftingSearchEntry.Text);
        if (_selectedCraftItem is not null)
            await SearchCraftingAsync();
    }

    private async Task SearchCraftingSuggestionsAsync(string query)
    {
        query = query.Trim();
        var currentVersion = ++_craftingSearchVersion;
        if (query.Length < 2)
        {
            _craftingSuggestionsLayout.Children.Clear();
            _craftingSuggestionsLayout.IsVisible = false;
            return;
        }

        await Task.Delay(180);
        if (currentVersion != _craftingSearchVersion)
            return;

        try
        {
            var apiQuery = UiText.SearchQueryForApi(query, _language);
            var items = await _apiClient.SearchItemsAsync(GetBaseUrl(), apiQuery, GetCraftingTierText(), GetCraftingEnchant());
            if (currentVersion != _craftingSearchVersion)
                return;

            _craftingSuggestionsLayout.Children.Clear();
            foreach (var item in items.Take(12))
                _craftingSuggestionsLayout.Children.Add(CreateCraftingItemButton(item));
            _craftingSuggestionsLayout.IsVisible = _craftingSuggestionsLayout.Children.Count > 0;
            _statusLabel.Text = _craftingSuggestionsLayout.Children.Count > 0
                ? T($"{_craftingSuggestionsLayout.Children.Count} item(ns) para craft.", $"{_craftingSuggestionsLayout.Children.Count} craft item(s).")
                : T("Nenhum item de craft encontrado.", "No craft item found.");
        }
        catch (Exception ex)
        {
            _craftingSuggestionsLayout.Children.Clear();
            _craftingSuggestionsLayout.IsVisible = false;
            _statusLabel.Text = T($"Erro na busca de craft: {ex.Message}", $"Craft search error: {ex.Message}");
        }
    }

    private async Task SearchCraftingAsync()
    {
        if (_selectedCraftItem is null)
        {
            _statusLabel.Text = T("Selecione um item para craft primeiro.", "Select a craft item first.");
            return;
        }

        Preferences.Set(BaseUrlPreferenceKey, GetBaseUrl());
        _craftingButton.IsEnabled = false;
        _craftingButton.Text = T("Calculando...", "Calculating...");
        _craftingResultLayout.Children.Clear();
        _statusLabel.Text = T("Calculando craft com cache de 15 minutos...", "Calculating craft with 15-minute cache...");

        try
        {
            var quote = await _apiClient.GetCraftingQuoteAsync(
                GetBaseUrl(),
                _selectedCraftItem.ItemId,
                GetCraftingTierNumber(),
                GetCraftingEnchant(),
                GetCraftingQuality(),
                GetSelectedServerKey(),
                _includeCaerleonInCrafting);
            UpdateCraftingResult(quote);
        }
        catch (Exception ex)
        {
            _craftingResultLayout.Children.Add(CreateCard(new Label
            {
                Text = T($"Erro no craft: {ex.Message}", $"Craft error: {ex.Message}"),
                TextColor = Color.FromArgb("#FFB86B"),
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            }, Color.FromArgb("#7A3D32"), Color.FromArgb("#211416")));
            _statusLabel.Text = T("Erro ao calcular craft.", "Error calculating craft.");
        }
        finally
        {
            _craftingButton.IsEnabled = true;
            _craftingButton.Text = T("Calcular craft", "Calculate craft");
        }
    }

    private void UpdateCraftingResult(CraftingQuote? quote)
    {
        _craftingResultLayout.Children.Clear();
        if (quote is null)
        {
            _craftingResultLayout.Children.Add(CreateCard(new Label
            {
                Text = T("Nenhuma resposta de craft.", "No craft response."),
                TextColor = Color.FromArgb("#FFB86B"),
                FontSize = 13
            }, Color.FromArgb("#7A3D32"), Color.FromArgb("#211416")));
            return;
        }

        var itemName = _language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(quote.ItemNamePtBr)
            ? quote.ItemNamePtBr
            : (_language == AppLanguage.PtBr ? UiText.TranslateItemNamePublic(quote.ItemName, quote.ItemId, _language) : quote.ItemName);
        var hasProfit = quote.ProfitWithPremium > 0;
        var border = hasProfit ? Color.FromArgb("#4FE18A") : Color.FromArgb("#FFB86B");
        var background = hasProfit ? Color.FromArgb("#102A1D") : Color.FromArgb("#221B13");
        var title = hasProfit
            ? T("Craft com lucro", "Profitable craft")
            : T("Sem lucro no momento", "No profit right now");

        var summary = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = title, TextColor = border, FontSize = 18, FontAttributes = FontAttributes.Bold },
                new Label { Text = itemName, TextColor = Colors.White, FontSize = 15, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap },
                new Label
                {
                    Text = T(
                        $"Custo dos ingredientes: {quote.TotalIngredientCost:N0}\nBônus de craft: {quote.CraftingCity}\nVenda: {quote.BestSellCity} • {quote.BestSellPrice:N0}\nTaxa premium: {quote.SaleTaxWithPremium:N0} • taxa sem premium: {quote.SaleTaxWithoutPremium:N0}\nCom premium: {FormatProfit(quote.ProfitWithPremium)} ({FormatProfitPercent(quote.ProfitPercentWithPremium)})\nSem premium: {FormatProfit(quote.ProfitWithoutPremium)} ({FormatProfitPercent(quote.ProfitPercentWithoutPremium)})",
                        $"Ingredient cost: {quote.TotalIngredientCost:N0}\nCraft bonus: {quote.CraftingCity}\nSell: {quote.BestSellCity} • {quote.BestSellPrice:N0}\nPremium tax: {quote.SaleTaxWithPremium:N0} • no-premium tax: {quote.SaleTaxWithoutPremium:N0}\nWith premium: {FormatProfit(quote.ProfitWithPremium)} ({FormatProfitPercent(quote.ProfitPercentWithPremium)})\nNo premium: {FormatProfit(quote.ProfitWithoutPremium)} ({FormatProfitPercent(quote.ProfitPercentWithoutPremium)})"),
                    TextColor = Color.FromArgb("#DCEBFF"),
                    FontSize = 13,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label
                {
                    Text = T($"Atualizado às {quote.UpdatedAtText} UTC • válido até {quote.ValidUntilText} UTC", $"Updated at {quote.UpdatedAtText} UTC • valid until {quote.ValidUntilText} UTC"),
                    TextColor = Color.FromArgb("#8FA2B8"),
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };
        _craftingResultLayout.Children.Add(CreateCard(summary, border, background));

        if (quote.Ingredients.Count > 0)
        {
            _craftingResultLayout.Children.Add(CreateLocalizedLabel(() => T("Comprar ingredientes", "Buy ingredients"), Colors.White, 17, FontAttributes.Bold));
            foreach (var ingredient in quote.Ingredients)
                _craftingResultLayout.Children.Add(CreateCraftingIngredientCard(ingredient));
        }
        else if (!string.IsNullOrWhiteSpace(quote.Message))
        {
            _craftingResultLayout.Children.Add(CreateCard(new Label
            {
                Text = quote.Message,
                TextColor = Color.FromArgb("#FFB86B"),
                FontSize = 13,
                LineBreakMode = LineBreakMode.WordWrap
            }, Color.FromArgb("#7A3D32"), Color.FromArgb("#211416")));
        }

        _statusLabel.Text = quote.HasOpportunityWithPremium
            ? T("Craft calculado com lucro líquido.", "Craft calculated with net profit.")
            : T("Craft calculado sem lucro líquido nos melhores preços atuais.", "Craft calculated without net profit at current best prices.");
    }

    private View CreateCraftingIngredientCard(CraftingIngredientCost ingredient)
    {
        var name = _language == AppLanguage.PtBr && !string.IsNullOrWhiteSpace(ingredient.ItemNamePtBr)
            ? ingredient.ItemNamePtBr
            : (_language == AppLanguage.PtBr ? UiText.TranslateItemNamePublic(ingredient.ItemName, ingredient.ItemId, _language) : ingredient.ItemName);

        var content = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = name, TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap },
                new Label
                {
                    Text = T(
                        $"{ingredient.Quantity}x em {ingredient.City} • unidade {ingredient.UnitPrice:N0} • custo ajustado {ingredient.TotalPrice:N0}",
                        $"{ingredient.Quantity}x in {ingredient.City} • unit {ingredient.UnitPrice:N0} • adjusted cost {ingredient.TotalPrice:N0}"),
                    TextColor = Color.FromArgb("#C8D5E3"),
                    FontSize = 12,
                    LineBreakMode = LineBreakMode.WordWrap
                },
                new Label { Text = T($"Atualizado às {ingredient.UpdatedAtText} UTC", $"Updated at {ingredient.UpdatedAtText} UTC"), TextColor = Color.FromArgb("#6F8399"), FontSize = 11, HorizontalTextAlignment = TextAlignment.End }
            }
        };

        return CreateCard(content, Color.FromArgb("#25364A"), Color.FromArgb("#101B28"));
    }

    private async Task RefreshGoldAsync()
    {
        _nextGoldRefreshUtc = NextAlbionPriceRefreshWindowUtc();
        try
        {
            var gold = await _apiClient.GetGoldAsync(GetBaseUrl(), GetSelectedServerKey());
            if (gold is null || gold.Price <= 0)
            {
                _lastGoldPrice = 0;
                _goldPriceLabel.Text = T("🟡 Ouro: indisponível", "🟡 Gold: unavailable");
                _premiumPriceLabel.Text = T("💎 Premium 30 dias: indisponível", "💎 30-day premium: unavailable");
                return;
            }

            _lastGoldPrice = gold.Price;
            var updated = gold.UpdatedAtUtc.HasValue ? gold.UpdatedAtUtc.Value.UtcDateTime.ToString("HH:mm") : "-";
            var premiumSilver = gold.Price * Premium30DaysGoldCost;
            _goldPriceLabel.Text = T(
                $"🟡 1 ouro = 🪙 {gold.PriceText} prata • {updated} UTC",
                $"🟡 1 gold = 🪙 {gold.PriceText} silver • {updated} UTC");
            _premiumPriceLabel.Text = T(
                $"💎 Premium 30 dias ≈ 🪙 {premiumSilver:N0} prata",
                $"💎 30-day premium ≈ 🪙 {premiumSilver:N0} silver");
        }
        catch
        {
            _lastGoldPrice = 0;
            _goldPriceLabel.Text = T("🟡 Ouro: indisponível", "🟡 Gold: unavailable");
            _premiumPriceLabel.Text = T("💎 Premium 30 dias: indisponível", "💎 30-day premium: unavailable");
        }
    }

    private void StartLiveTimers()
    {
        if (_clockTimer is not null)
        {
            _clockTimer.Start();
            return;
        }

        _clockTimer = Dispatcher.CreateTimer();
        _clockTimer.Interval = TimeSpan.FromSeconds(30);
        _clockTimer.Tick += async (_, _) =>
        {
            _serverTimeHeaderLabel.Text = GetServerTimeText();
            _cacheInfoLabel.Text = GetCacheInfoText();
            _refiningFooterLabel.Text = GetCacheInfoText();
            if (DateTimeOffset.UtcNow >= _nextGoldRefreshUtc)
                await RefreshGoldAsync();
        };
        _clockTimer.Start();
    }

    private string GetServerTimeText()
    {
        var utc = DateTimeOffset.UtcNow;
        return T($"Horário do servidor: {utc:HH:mm} UTC", $"Server time: {utc:HH:mm} UTC");
    }

    private string GetBaseUrl() => Preferences.Get(BaseUrlPreferenceKey, DefaultApiBaseUrl).Trim().TrimEnd('/');
    private string GetQuickTierText() => _quickTierPicker.SelectedItem?.ToString() ?? "T4";
    private string GetCategoryTierText() => _tierPicker.SelectedItem?.ToString() ?? "T4";
    private string GetCraftingTierText() => _craftingTierPicker.SelectedItem?.ToString() ?? "T4";
    private int GetQuickTierNumber() => ParseTier(GetQuickTierText());
    private int GetCategoryTierNumber() => ParseTier(GetCategoryTierText());
    private int GetCraftingTierNumber() => ParseTier(GetCraftingTierText());
    private int GetQuickEnchant() => Math.Max(0, _quickEnchantPicker.SelectedIndex);
    private int GetCategoryEnchant() => Math.Max(0, _enchantPicker.SelectedIndex);
    private int GetCraftingEnchant() => Math.Max(0, _craftingEnchantPicker.SelectedIndex);
    private int GetQuickQuality() => Math.Clamp(_quickQualityPicker.SelectedIndex + 1, 1, 5);
    private int GetCategoryQuality() => Math.Clamp(_qualityPicker.SelectedIndex + 1, 1, 5);
    private int GetCraftingQuality() => Math.Clamp(_craftingQualityPicker.SelectedIndex + 1, 1, 5);
    private int GetActiveTierNumber() => _selectionMode == SelectionMode.QuickSearch ? GetQuickTierNumber() : GetCategoryTierNumber();
    private int GetActiveEnchant() => _selectionMode == SelectionMode.QuickSearch ? GetQuickEnchant() : GetCategoryEnchant();
    private int GetActiveQuality() => _selectionMode == SelectionMode.QuickSearch ? GetQuickQuality() : GetCategoryQuality();

    private static int ParseTier(string tierText) => int.TryParse((tierText ?? "T4").TrimStart('T', 't'), out var tier) ? tier : 4;

    private void SetActiveFilters(int tier, int enchant, int quality)
    {
        var tierIndex = Math.Clamp(tier - 4, 0, 4);
        enchant = Math.Clamp(enchant, 0, 4);
        quality = Math.Clamp(quality, 1, 5) - 1;
        if (_selectionMode == SelectionMode.QuickSearch)
        {
            _quickTierPicker.SelectedIndex = tierIndex;
            _quickEnchantPicker.SelectedIndex = enchant;
            _quickQualityPicker.SelectedIndex = quality;
        }
        else
        {
            _tierPicker.SelectedIndex = tierIndex;
            _enchantPicker.SelectedIndex = enchant;
            _qualityPicker.SelectedIndex = quality;
        }
    }

    private void PopulateQualityPicker(Picker picker, int selectedIndex)
    {
        picker.Items.Clear();
        for (var q = 1; q <= 5; q++)
            picker.Items.Add(UiText.QualityName(q, _language));
        picker.SelectedIndex = Math.Clamp(selectedIndex, 0, 4);
    }

    private View CreateCatalogGrid()
    {
        var grid = new Grid { ColumnSpacing = 10, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        var categorySection = CreateSection(() => T("Categoria", "Category"), _categoryPicker);
        var groupSection = CreateSection(() => T("Grupo", "Group"), _groupPicker);
        Grid.SetColumn(categorySection, 0);
        Grid.SetColumn(groupSection, 1);
        grid.Children.Add(categorySection);
        grid.Children.Add(groupSection);
        return grid;
    }

    private View CreateQuickFiltersGrid()
    {
        return CreateThreeColumnGrid(
            CreateSection(() => "Tier", _quickTierPicker),
            CreateSection(() => T("Encant.", "Enchant"), _quickEnchantPicker),
            CreateSection(() => T("Qualidade", "Quality"), _quickQualityPicker));
    }

    private View CreateCategoryFiltersGrid()
    {
        return CreateThreeColumnGrid(
            CreateSection(() => "Tier", _tierPicker),
            CreateSection(() => T("Encant.", "Enchant"), _enchantPicker),
            CreateSection(() => T("Qualidade", "Quality"), _qualityPicker));
    }

    private static Grid CreateThreeColumnGrid(View first, View second, View third)
    {
        var grid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
        };
        Grid.SetColumn(first, 0);
        Grid.SetColumn(second, 1);
        Grid.SetColumn(third, 2);
        grid.Children.Add(first);
        grid.Children.Add(second);
        grid.Children.Add(third);
        return grid;
    }

    private View CreateSection(Func<string> titleFactory, View content)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                CreateLocalizedLabel(titleFactory, Color.FromArgb("#8FA2B8"), 12, FontAttributes.Bold),
                content
            }
        };
    }

    private View CreateSectionTitle(Func<string> titleFactory, Func<string> subtitleFactory)
    {
        return new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                CreateLocalizedLabel(titleFactory, Colors.White, 18, FontAttributes.Bold),
                CreateLocalizedLabel(subtitleFactory, Color.FromArgb("#8FA2B8"), 13)
            }
        };
    }

    private Label CreateLocalizedLabel(Func<string> textFactory, Color textColor, double fontSize, FontAttributes attributes = FontAttributes.None, Thickness? margin = null)
    {
        var label = new Label
        {
            Text = textFactory(),
            TextColor = textColor,
            FontSize = fontSize,
            FontAttributes = attributes,
            Margin = margin ?? Thickness.Zero,
            LineBreakMode = LineBreakMode.WordWrap
        };
        _textRefreshers.Add(() => label.Text = textFactory());
        return label;
    }

    private static View CreateCard(View content, Color borderColor, Color backgroundColor)
    {
        return new Frame
        {
            Padding = 14,
            CornerRadius = 20,
            HasShadow = false,
            BorderColor = borderColor,
            BackgroundColor = backgroundColor,
            Content = content
        };
    }

    private string GetCacheInfoText()
    {
        var validUntil = NextAlbionPriceRefreshWindowUtc().UtcDateTime.ToString("HH:mm");
        return T($"Preços sincronizados a cada 15 minutos • válido até {validUntil} UTC", $"Prices synchronized every 15 minutes • valid until {validUntil} UTC");
    }

    private static DateTimeOffset NextAlbionPriceRefreshWindowUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var nextMinute = ((now.Minute / 15) + 1) * 15;
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero).AddMinutes(nextMinute);
        if (next <= now.AddSeconds(30))
            next = next.AddMinutes(15);
        return next;
    }

    private string T(string pt, string en) => _language == AppLanguage.En ? en : pt;
    private static bool SameCity(string a, string b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static string ApplyTierAndEnchant(string itemId, int tier, int enchant)
    {
        if (string.IsNullOrWhiteSpace(itemId))
            return itemId;
        var withoutEnchant = itemId.Split('@')[0];
        var replaced = System.Text.RegularExpressions.Regex.Replace(withoutEnchant, "^T[1-8]_", $"T{tier}_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return enchant > 0 ? $"{replaced}@{enchant}" : replaced;
    }

    private static string GetItemFamilyKey(string itemId)
    {
        var clean = itemId.Split('@')[0].ToUpperInvariant();
        clean = System.Text.RegularExpressions.Regex.Replace(clean, "^T[1-8]_", "T?_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        return clean;
    }

    private static bool SameFamily(string itemId, string familyKey) => GetItemFamilyKey(itemId).Equals(familyKey, StringComparison.OrdinalIgnoreCase);
}
