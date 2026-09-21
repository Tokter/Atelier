using System;
using System.ComponentModel;
using Atelier.Controls;
using Atelier.Core.Primitives;
using Atelier.Core.Properties;
using Atelier.Core.Tree;
using Atelier.Core.ViewResolution;
using Atelier.Layout;
using Atelier.Markup;
using Xunit;

namespace Atelier.Tests;

// Test ViewModels and Views
public class MasterTestViewModel : INotifyPropertyChanged
{
    private object? _currentDetail;
    public object? CurrentDetail
    {
        get => _currentDetail;
        set
        {
            if (_currentDetail != value)
            {
                _currentDetail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentDetail)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public interface IBaseDetailViewModel { string Name { get; } }

public class ProfileDetailViewModel : INotifyPropertyChanged, IBaseDetailViewModel
{
    private string _userName;
    public string Name => "Profile";

    public string UserName
    {
        get => _userName;
        set
        {
            if (_userName != value)
            {
                _userName = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UserName)));
            }
        }
    }

    public ProfileDetailViewModel(string userName)
    {
        _userName = userName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SettingsDetailViewModel : INotifyPropertyChanged, IBaseDetailViewModel
{
    private bool _notificationsEnabled;
    public string Name => "Settings";

    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set
        {
            if (_notificationsEnabled != value)
            {
                _notificationsEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NotificationsEnabled)));
            }
        }
    }

    public SettingsDetailViewModel(bool notificationsEnabled)
    {
        _notificationsEnabled = notificationsEnabled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

// Corresponding Views
public class ProfileDetailView : StackPanel
{
    public TextBlock TitleText { get; }
    public TextBlock UserText { get; }

    public ProfileDetailView()
    {
        TitleText = new TextBlock("User Profile");
        UserText = new TextBlock();
        UserText.BindText<ProfileDetailViewModel>(vm => vm.UserName);
        Add(TitleText);
        Add(UserText);
    }
}

public class SettingsDetailView : StackPanel
{
    public TextBlock TitleText { get; }

    public SettingsDetailView()
    {
        TitleText = new TextBlock("Application Settings");
        Add(TitleText);
    }
}

// Convention lookup test pair (Name ends in ViewModel and View in the same namespace)
public class ConventionSampleViewModel
{
    public string Message { get; set; } = "Hello Convention";
}

public class ConventionSampleView : StackPanel
{
    public TextBlock Label { get; } = new("Convention View Content");
    public ConventionSampleView()
    {
        Add(Label);
    }
}

public class TypedCtorViewModel
{
    public string Message { get; }
    public TypedCtorViewModel(string msg) => Message = msg;
}

public class TypedCtorView : Border
{
    public TypedCtorViewModel ViewModel { get; }
    public TypedCtorView(TypedCtorViewModel vm)
    {
        ViewModel = vm;
    }
}

public class ViewLocatorAndContentControlTests
{
    [Fact]
    public void ViewLocator_ExplicitRegistration_ResolvesRegisteredView()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();
        locator.Register<SettingsDetailViewModel>(vm => new SettingsDetailView());

        var profileVm = new ProfileDetailViewModel("Alice");
        var settingsVm = new SettingsDetailViewModel(true);

        Assert.True(locator.CanResolve(profileVm));
        Assert.True(locator.CanResolve(settingsVm));

        var profileView = locator.ResolveView(profileVm);
        Assert.NotNull(profileView);
        Assert.IsType<ProfileDetailView>(profileView);

        var settingsView = locator.ResolveView(settingsVm);
        Assert.NotNull(settingsView);
        Assert.IsType<SettingsDetailView>(settingsView);
    }

    [Fact]
    public void ViewLocator_PolymorphicRegistration_ResolvesDerivedInstances()
    {
        var locator = new ViewLocator();
        locator.Register<IBaseDetailViewModel>(vm => new TextBlock($"Resolved from interface: {vm.Name}"));

        var profileVm = new ProfileDetailViewModel("Bob");
        Assert.True(locator.CanResolve(profileVm));

        var view = locator.ResolveView(profileVm);
        Assert.NotNull(view);
        Assert.IsType<TextBlock>(view);
        Assert.Equal("Resolved from interface: Profile", ((TextBlock)view).Text);
    }

    [Fact]
    public void ViewLocator_ConventionLookup_ResolvesMatchingView()
    {
        var locator = new ViewLocator { EnableConventionLookup = true };

        var vm = new ConventionSampleViewModel();
        Assert.True(locator.CanResolve(vm));

        var view = locator.ResolveView(vm);
        Assert.NotNull(view);
        Assert.IsType<ConventionSampleView>(view);
    }

    [Fact]
    public void ViewLocator_ConventionLookup_UsesViewModelConstructorIfAvailable()
    {
        var locator = new ViewLocator { EnableConventionLookup = true };

        var vm = new TypedCtorViewModel("Hello Convention");
        Assert.True(locator.CanResolve(vm));

        var view = locator.ResolveView(vm);
        Assert.NotNull(view);
        var typedView = Assert.IsType<TypedCtorView>(view);
        Assert.Same(vm, typedView.ViewModel);
    }

    [Fact]
    public void ContentControl_DirectUIElement_BypassesLocator()
    {
        var button = new Button("Click Me");
        var contentControl = new ContentControl
        {
            Content = button
        };

        Assert.Same(button, contentControl.CurrentView);
        Assert.Contains(button, contentControl.Children);
    }

    [Fact]
    public void ContentControl_ViewModelBinding_ResolvesViewAndSetsDataContext()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();

        var masterVm = new MasterTestViewModel();
        var profileVm = new ProfileDetailViewModel("Charlie");
        masterVm.CurrentDetail = profileVm;

        var contentControl = new ContentControl
        {
            ViewLocator = locator
        };
        contentControl.BindContent(masterVm, m => m.CurrentDetail);

        // View should be resolved and mounted
        Assert.NotNull(contentControl.CurrentView);
        Assert.IsType<ProfileDetailView>(contentControl.CurrentView);
        Assert.Contains(contentControl.CurrentView, contentControl.Children);

        // DataContext on view must be set to the ViewModel
        Assert.Same(profileVm, contentControl.CurrentView.DataContext);

        // Child binding inside view should have evaluated
        var profileView = (ProfileDetailView)contentControl.CurrentView;
        Assert.Equal("Charlie", profileView.UserText.Text);
    }

    [Fact]
    public void ContentControl_UpdatingViewModel_ReplacesViewWithNewMatchingView()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();
        locator.Register<SettingsDetailViewModel, SettingsDetailView>();

        var masterVm = new MasterTestViewModel();
        var profileVm = new ProfileDetailViewModel("Diana");
        var settingsVm = new SettingsDetailViewModel(false);

        var contentControl = new ContentControl
        {
            ViewLocator = locator
        };
        contentControl.BindContent(masterVm, m => m.CurrentDetail);

        // 1. Initial state: Profile
        masterVm.CurrentDetail = profileVm;
        Assert.IsType<ProfileDetailView>(contentControl.CurrentView);
        var initialView = contentControl.CurrentView;
        Assert.Same(profileVm, initialView.DataContext);

        // 2. Update to Settings
        masterVm.CurrentDetail = settingsVm;
        Assert.NotNull(contentControl.CurrentView);
        Assert.IsType<SettingsDetailView>(contentControl.CurrentView);
        Assert.NotSame(initialView, contentControl.CurrentView);
        Assert.DoesNotContain(initialView, contentControl.Children);
        Assert.Contains(contentControl.CurrentView, contentControl.Children);
        Assert.Same(settingsVm, contentControl.CurrentView.DataContext);

        // 3. Update to another Profile
        var profileVm2 = new ProfileDetailViewModel("Evan");
        masterVm.CurrentDetail = profileVm2;
        Assert.IsType<ProfileDetailView>(contentControl.CurrentView);
        Assert.Same(profileVm2, contentControl.CurrentView.DataContext);
        var profileView2 = (ProfileDetailView)contentControl.CurrentView;
        Assert.Equal("Evan", profileView2.UserText.Text);
    }

    [Fact]
    public void ContentControl_SetContentNull_ClearsPresentedView()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();

        var masterVm = new MasterTestViewModel();
        masterVm.CurrentDetail = new ProfileDetailViewModel("Fiona");

        var contentControl = new ContentControl
        {
            ViewLocator = locator
        };
        contentControl.BindContent(masterVm, m => m.CurrentDetail);

        Assert.NotNull(contentControl.CurrentView);
        Assert.Single(contentControl.Children);

        // Set to null
        masterVm.CurrentDetail = null;
        Assert.Null(contentControl.CurrentView);
        Assert.Empty(contentControl.Children);
    }

    [Fact]
    public void ContentControl_ContentTemplate_OverridesViewLocator()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();

        var vm = new ProfileDetailViewModel("George");
        var customTemplateView = new TextBlock("Custom Template Override");

        var contentControl = new ContentControl
        {
            ViewLocator = locator,
            ContentTemplate = data => customTemplateView,
            Content = vm
        };

        // ContentTemplate takes precedence over ViewLocator
        Assert.Same(customTemplateView, contentControl.CurrentView);
        Assert.Same(vm, customTemplateView.DataContext);
    }

    [Fact]
    public void ContentControl_LocalViewLocatorOverride_TakesPrecedenceOverGlobal()
    {
        // Global locator has one factory
        ViewLocator.Current.Register<ProfileDetailViewModel>(_ => new TextBlock("Global"));

        // Local locator has a different factory
        var localLocator = new ViewLocator();
        localLocator.Register<ProfileDetailViewModel>(_ => new TextBlock("Local"));

        var contentControl = new ContentControl
        {
            ViewLocator = localLocator,
            Content = new ProfileDetailViewModel("Hannah")
        };

        Assert.NotNull(contentControl.CurrentView);
        Assert.IsType<TextBlock>(contentControl.CurrentView);
        Assert.Equal("Local", ((TextBlock)contentControl.CurrentView).Text);
    }

    [Fact]
    public void ContentControl_LayoutPass_MeasuresAndArrangesPresentedView()
    {
        var locator = new ViewLocator();
        locator.Register<ProfileDetailViewModel, ProfileDetailView>();

        var contentControl = new ContentControl
        {
            ViewLocator = locator,
            Padding = new Thickness(10),
            Content = new ProfileDetailViewModel("Ian")
        };

        contentControl.Measure(new Size(500, 300));
        contentControl.Arrange(new Rect(0, 0, 500, 300));

        Assert.NotNull(contentControl.CurrentView);
        Assert.True(contentControl.CurrentView.Bounds.Width > 0);
        Assert.True(contentControl.CurrentView.Bounds.Height > 0);
        Assert.Equal(10f, contentControl.CurrentView.Bounds.X);
        Assert.Equal(10f, contentControl.CurrentView.Bounds.Y);
    }
}
