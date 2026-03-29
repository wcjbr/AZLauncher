using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AZLauncher.Models;
using AZLauncher.Services;

namespace AZLauncher.ViewModels;

public enum SettingsCategory
{
    Runtime,
    Instances,
    Interface,
}

public enum SettingsPane
{
    Java,
    Memory,
    Folders,
    Storage,
    Branding,
    Customize,
}

public sealed partial class SettingsPageViewModel : LocalizedViewModelBase
{
    private readonly LauncherStateService launcherState;
    private readonly AppConfigService configService;
    private readonly JavaDiscoveryService javaDiscoveryService;

    public SettingsPageViewModel(
        LocalizationService localizer,
        LauncherStateService launcherState,
        ThemeCustomizationService themeService,
        AppConfigService configService) : base(localizer)
    {
        this.launcherState = launcherState;
        this.configService = configService;
        javaDiscoveryService = new JavaDiscoveryService();
        this.launcherState.PropertyChanged += HandleLauncherStateChanged;
        this.configService.PropertyChanged += HandleConfigPropertyChanged;
        CustomizePage = new CustomizePageViewModel(localizer, themeService, configService);
        selectedMemoryOption = configService.DefaultMemoryGb;
        RefreshFolderItems();
    }

    public CustomizePageViewModel CustomizePage { get; }

    public IReadOnlyList<int> MemoryOptions { get; } = [2, 4, 6, 8, 10, 12, 16, 24, 32];

    public string SectionLabel => IsChinese ? "设置中心" : "Settings";

    public string SectionHeading => IsChinese ? "统一管理运行时、目录和外观" : "Manage runtime, folders, and appearance together";

    public string SectionSummary => IsChinese
        ? "主导航进入设置后，可以继续下钻到二级分类和三级菜单。"
        : "Enter settings from the main navigation, then drill down into secondary groups and third-level menus.";

    public string CategoriesLabel => IsChinese ? "设置分组" : "Settings groups";

    public string RuntimeCategoryLabel => IsChinese ? "运行时" : "Runtime";

    public string InstancesCategoryLabel => IsChinese ? "实例目录" : "Instance folders";

    public string InterfaceCategoryLabel => IsChinese ? "界面" : "Interface";

    public string SubmenuLabel => IsChinese ? "三级菜单" : "Third-level menu";

    public string JavaPaneLabel => IsChinese ? "默认 Java" : "Default Java";

    public string MemoryPaneLabel => IsChinese ? "默认内存" : "Default memory";

    public string FoldersPaneLabel => IsChinese ? "文件夹选择" : "Folder picker";

    public string StoragePaneLabel => IsChinese ? "存储状态" : "Storage state";

    public string BrandingPaneLabel => IsChinese ? "品牌标题" : "Branding";

    public string CustomizePaneLabel => IsChinese ? "界面自定义" : "Customize";

    public string CurrentPaneTitle => CurrentPane switch
    {
        SettingsPane.Java => IsChinese ? "配置默认 Java 运行时" : "Configure the default Java runtime",
        SettingsPane.Memory => IsChinese ? "配置默认内存分配" : "Configure default memory allocation",
        SettingsPane.Folders => IsChinese ? "管理多个 MC 实例目录" : "Manage multiple MC instance folders",
        SettingsPane.Storage => IsChinese ? "查看当前目录的存储状态" : "Inspect storage for the active folder",
        SettingsPane.Branding => IsChinese ? "设置启动器标题" : "Set the launcher title",
        SettingsPane.Customize => IsChinese ? "把自定义迁移到设置内部" : "Keep customization inside settings",
        _ => string.Empty,
    };

    public string CurrentPaneSummary => CurrentPane switch
    {
        SettingsPane.Java => IsChinese
            ? "这里会作为所有实例默认使用的 Java 配置。"
            : "This becomes the default Java configuration for all instances.",
        SettingsPane.Memory => IsChinese
            ? "这里设置所有实例共用的默认内存分配策略。"
            : "This defines the shared default memory budget for all instances.",
        SettingsPane.Folders => IsChinese
            ? "可添加多个目录，并选定其中一个作为当前实例根目录。"
            : "Add multiple folders and choose one as the active instance root.",
        SettingsPane.Storage => IsChinese
            ? "当前可用空间会随活动实例目录切换而更新。"
            : "Available space updates whenever the active instance folder changes.",
        SettingsPane.Branding => IsChinese
            ? "这里修改 AZLauncher 标题，并持久化到本地配置文件。"
            : "Change the AZLauncher title here and persist it to the local config file.",
        SettingsPane.Customize => IsChinese
            ? "原有自定义功能已迁入这里，主题、密度和圆角仍然实时生效。"
            : "The previous customization page now lives here with live theme, density, and shape changes.",
        _ => string.Empty,
    };

    public string JavaInputLabel => IsChinese ? "默认 Java" : "Default Java";

    public string JavaInputSummary => IsChinese
        ? "可以填版本名、运行时名称或完整可执行路径。"
        : "Use a version label, runtime name, or a full executable path.";

    public string DetectJavaLabel => IsChinese ? "探测 Java" : "Detect Java";

    public string DetectedJavaLabel => IsChinese ? "检测结果" : "Detected runtimes";

    public string UseDetectedJavaLabel => IsChinese ? "使用此项" : "Use runtime";

    public string JavaDetectionStatus => string.IsNullOrWhiteSpace(JavaDetectionMessage)
        ? (IsChinese ? "尚未执行探测。" : "No detection has been run yet.")
        : JavaDetectionMessage;

    public string MemoryInputLabel => IsChinese ? "默认内存" : "Default memory";

    public string MemoryInputSummary => IsChinese
        ? "选择默认分配给实例的内存容量。"
        : "Choose the default amount of memory assigned to instances.";

    public string FolderInputLabel => IsChinese ? "新增实例目录" : "Add instance folder";

    public string FolderInputSummary => IsChinese
        ? "可以手动输入路径，也可以用目录选择器添加。"
        : "Add a folder manually or use the folder picker.";

    public string FolderInputWatermark => IsChinese ? "/home/user/Games/Minecraft" : "/home/user/Games/Minecraft";

    public string BrowseFolderLabel => IsChinese ? "选择目录" : "Browse";

    public string AddFolderLabel => IsChinese ? "添加目录" : "Add folder";

    public string UseFolderLabel => IsChinese ? "设为当前" : "Set active";

    public string RemoveFolderLabel => IsChinese ? "移除" : "Remove";

    public string ActiveFolderLabel => IsChinese ? "当前目录" : "Active folder";

    public string FolderCountLabel => IsChinese ? "目录数量" : "Folder count";

    public string StorageValueLabel => IsChinese ? "可用空间" : "Available space";

    public string IsolationModeLabel => IsChinese ? "版本隔离" : "Version isolation";

    public string IsolationRootLabel => IsChinese ? "共享仓库" : "Shared store";

    public string BrandingSummary => IsChinese
        ? "窗口标题和侧边栏主标题会同时使用这里的内容。"
        : "The window title and sidebar title both use this value.";

    public string ConfigPathLabel => IsChinese ? "配置文件" : "Config file";

    public string LauncherTitleLabel => IsChinese ? "启动器标题" : "Launcher title";

    public string ActiveStatus => IsChinese ? "当前使用" : "Active";

    public string InactiveStatus => IsChinese ? "已收录" : "Indexed";

    public bool IsRuntimeCategorySelected => CurrentCategory == SettingsCategory.Runtime;

    public bool IsInstancesCategorySelected => CurrentCategory == SettingsCategory.Instances;

    public bool IsInterfaceCategorySelected => CurrentCategory == SettingsCategory.Interface;

    public bool IsRuntimeCategoryVisible => CurrentCategory == SettingsCategory.Runtime;

    public bool IsInstancesCategoryVisible => CurrentCategory == SettingsCategory.Instances;

    public bool IsInterfaceCategoryVisible => CurrentCategory == SettingsCategory.Interface;

    public bool IsJavaPaneSelected => CurrentPane == SettingsPane.Java;

    public bool IsMemoryPaneSelected => CurrentPane == SettingsPane.Memory;

    public bool IsFoldersPaneSelected => CurrentPane == SettingsPane.Folders;

    public bool IsStoragePaneSelected => CurrentPane == SettingsPane.Storage;

    public bool IsBrandingPaneSelected => CurrentPane == SettingsPane.Branding;

    public bool IsCustomizePaneSelected => CurrentPane == SettingsPane.Customize;

    public string DefaultJavaRuntime
    {
        get => configService.DefaultJavaRuntime;
        set => configService.DefaultJavaRuntime = value;
    }

    public string LauncherTitle
    {
        get => configService.LauncherTitle;
        set => configService.LauncherTitle = value;
    }

    public string ActiveInstanceFolder => configService.ActiveInstanceFolder;

    public string ConfigPath => configService.ConfigFilePath;

    public string StorageValue => launcherState.FormatStorage(IsChinese);

    public string VersionIsolationMode => IsChinese
        ? launcherState.VersionIsolationSummaryZh
        : launcherState.VersionIsolationSummary;

    public string VersionIsolationRoot => launcherState.VersionIsolationRoot;

    [ObservableProperty]
    private SettingsCategory currentCategory = SettingsCategory.Runtime;

    [ObservableProperty]
    private SettingsPane currentPane = SettingsPane.Java;

    [ObservableProperty]
    private int selectedMemoryOption;

    [ObservableProperty]
    private string pendingInstanceFolder = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<InstanceFolderItem> instanceFolderItems = [];

    [ObservableProperty]
    private IReadOnlyList<JavaRuntimeCandidate> detectedJavaRuntimes = [];

    [ObservableProperty]
    private bool isDetectingJava;

    [ObservableProperty]
    private string javaDetectionMessage = string.Empty;

    partial void OnSelectedMemoryOptionChanged(int value)
    {
        if (value > 0 && configService.DefaultMemoryGb != value)
        {
            configService.DefaultMemoryGb = value;
        }
    }

    protected override void OnLanguageChanged()
    {
        RefreshFolderItems();
        RaiseProperties(
            nameof(SectionLabel),
            nameof(SectionHeading),
            nameof(SectionSummary),
            nameof(CategoriesLabel),
            nameof(RuntimeCategoryLabel),
            nameof(InstancesCategoryLabel),
            nameof(InterfaceCategoryLabel),
            nameof(SubmenuLabel),
            nameof(JavaPaneLabel),
            nameof(MemoryPaneLabel),
            nameof(FoldersPaneLabel),
            nameof(StoragePaneLabel),
            nameof(BrandingPaneLabel),
            nameof(CustomizePaneLabel),
            nameof(CurrentPaneTitle),
            nameof(CurrentPaneSummary),
            nameof(JavaInputLabel),
            nameof(JavaInputSummary),
            nameof(DetectJavaLabel),
            nameof(DetectedJavaLabel),
            nameof(UseDetectedJavaLabel),
            nameof(JavaDetectionStatus),
            nameof(MemoryInputLabel),
            nameof(MemoryInputSummary),
            nameof(FolderInputLabel),
            nameof(FolderInputSummary),
            nameof(FolderInputWatermark),
            nameof(BrowseFolderLabel),
            nameof(AddFolderLabel),
            nameof(UseFolderLabel),
            nameof(RemoveFolderLabel),
            nameof(ActiveFolderLabel),
            nameof(FolderCountLabel),
            nameof(StorageValueLabel),
            nameof(IsolationModeLabel),
            nameof(IsolationRootLabel),
            nameof(BrandingSummary),
            nameof(ConfigPathLabel),
            nameof(LauncherTitleLabel),
            nameof(ActiveStatus),
            nameof(InactiveStatus),
            nameof(StorageValue),
            nameof(VersionIsolationMode));
        RaiseSelectionProperties();
    }

    [RelayCommand]
    private void ShowRuntime()
    {
        CurrentCategory = SettingsCategory.Runtime;
        CurrentPane = SettingsPane.Java;
    }

    [RelayCommand]
    private void ShowInstances()
    {
        CurrentCategory = SettingsCategory.Instances;
        CurrentPane = SettingsPane.Folders;
    }

    [RelayCommand]
    private void ShowInterface()
    {
        CurrentCategory = SettingsCategory.Interface;
        CurrentPane = SettingsPane.Branding;
    }

    [RelayCommand]
    private void ShowJavaPane()
    {
        CurrentPane = SettingsPane.Java;
        if (DetectedJavaRuntimes.Count == 0)
        {
            _ = DetectJavaAsync();
        }
    }

    [RelayCommand]
    private void ShowMemoryPane()
    {
        CurrentPane = SettingsPane.Memory;
    }

    [RelayCommand]
    private void ShowFoldersPane()
    {
        CurrentPane = SettingsPane.Folders;
    }

    [RelayCommand]
    private void ShowStoragePane()
    {
        CurrentPane = SettingsPane.Storage;
    }

    [RelayCommand]
    private void ShowBrandingPane()
    {
        CurrentPane = SettingsPane.Branding;
    }

    [RelayCommand]
    private void ShowCustomizePane()
    {
        CurrentPane = SettingsPane.Customize;
    }

    [RelayCommand]
    private void AddFolder()
    {
        if (!configService.AddInstanceFolder(PendingInstanceFolder))
        {
            return;
        }

        configService.ActiveInstanceFolder = PendingInstanceFolder;
        PendingInstanceFolder = string.Empty;
    }

    [RelayCommand]
    private void UseFolder(string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            configService.ActiveInstanceFolder = folderPath;
        }
    }

    [RelayCommand]
    private void RemoveFolder(string? folderPath)
    {
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            configService.RemoveInstanceFolder(folderPath);
        }
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var desktop = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        var owner = desktop?.MainWindow;
        if (owner?.StorageProvider is null)
        {
            return;
        }

        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = IsChinese ? "选择 MC 实例文件夹" : "Choose an MC instance folder",
        });

        var localPath = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(localPath))
        {
            return;
        }

        configService.AddInstanceFolder(localPath);
        configService.ActiveInstanceFolder = localPath;
        PendingInstanceFolder = string.Empty;
    }

    [RelayCommand]
    private async Task DetectJavaAsync()
    {
        if (IsDetectingJava)
        {
            return;
        }

        try
        {
            IsDetectingJava = true;
            JavaDetectionMessage = IsChinese ? "正在探测本机 Java..." : "Detecting local Java runtimes...";
            DetectedJavaRuntimes = await javaDiscoveryService.DiscoverAsync();
            JavaDetectionMessage = DetectedJavaRuntimes.Count == 0
                ? (IsChinese ? "没有探测到可用的 Java 运行时。" : "No usable Java runtime was detected.")
                : (IsChinese ? $"已探测到 {DetectedJavaRuntimes.Count} 个 Java 运行时。" : $"Detected {DetectedJavaRuntimes.Count} Java runtime(s).");
        }
        catch (System.Exception ex)
        {
            JavaDetectionMessage = ex.Message;
        }
        finally
        {
            IsDetectingJava = false;
            RaiseProperties(nameof(JavaDetectionStatus));
        }
    }

    [RelayCommand]
    private void UseDetectedJava(JavaRuntimeCandidate? candidate)
    {
        if (candidate is null)
        {
            return;
        }

        DefaultJavaRuntime = candidate.ExecutablePath;
        JavaDetectionMessage = IsChinese
            ? $"已切换到 {candidate.VersionLabel}"
            : $"Switched to {candidate.VersionLabel}";
        RaiseProperties(nameof(JavaDetectionStatus));
    }

    partial void OnCurrentCategoryChanged(SettingsCategory value)
    {
        RaiseSelectionProperties();
    }

    partial void OnCurrentPaneChanged(SettingsPane value)
    {
        RaiseSelectionProperties();
        if (value == SettingsPane.Java && DetectedJavaRuntimes.Count == 0)
        {
            _ = DetectJavaAsync();
        }
    }

    private void RefreshFolderItems()
    {
        InstanceFolderItems = configService.InstanceFolders
            .Select(folder => new InstanceFolderItem
            {
                Path = folder,
                IsActive = string.Equals(folder, configService.ActiveInstanceFolder, System.StringComparison.OrdinalIgnoreCase),
                Status = string.Equals(folder, configService.ActiveInstanceFolder, System.StringComparison.OrdinalIgnoreCase)
                    ? ActiveStatus
                    : InactiveStatus,
            })
            .ToArray();
    }

    private void RaiseSelectionProperties()
    {
        RaiseProperties(
            nameof(CurrentPaneTitle),
            nameof(CurrentPaneSummary),
            nameof(IsRuntimeCategorySelected),
            nameof(IsInstancesCategorySelected),
            nameof(IsInterfaceCategorySelected),
            nameof(IsRuntimeCategoryVisible),
            nameof(IsInstancesCategoryVisible),
            nameof(IsInterfaceCategoryVisible),
            nameof(IsJavaPaneSelected),
            nameof(IsMemoryPaneSelected),
            nameof(IsFoldersPaneSelected),
            nameof(IsStoragePaneSelected),
            nameof(IsBrandingPaneSelected),
            nameof(IsCustomizePaneSelected));
    }

    private void HandleConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppConfigService.InstanceFolders) or nameof(AppConfigService.ActiveInstanceFolder))
        {
            RefreshFolderItems();
            RaiseProperties(nameof(ActiveInstanceFolder));
        }

        if (e.PropertyName == nameof(AppConfigService.DefaultMemoryGb))
        {
            SelectedMemoryOption = configService.DefaultMemoryGb;
        }

        if (e.PropertyName is nameof(AppConfigService.DefaultJavaRuntime)
            or nameof(AppConfigService.LauncherTitle)
            or nameof(AppConfigService.ConfigFilePath))
        {
            RaiseProperties(nameof(DefaultJavaRuntime), nameof(LauncherTitle), nameof(ConfigPath));
        }
    }

    private void HandleLauncherStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LauncherStateService.AvailableStorageBytes)
            or nameof(LauncherStateService.InstallPath)
            or nameof(LauncherStateService.VersionIsolationRoot))
        {
            RaiseProperties(nameof(StorageValue), nameof(VersionIsolationRoot));
        }
    }
}
