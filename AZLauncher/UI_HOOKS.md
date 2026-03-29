# AZLauncher UI Hooks

这份文档把当前项目里所有“界面相关 hooks / 入口 / 状态同步点”集中到一个文件里，方便后续继续扩展。

## 1. 总入口

### 应用启动入口

- `App.axaml.cs`
  - 创建 `AppConfigService`
  - 把配置服务注入 `MainWindowViewModel`
  - 由主窗口继续分发到语言、主题、自定义页面

### 主窗口壳层

- `Views/MainWindow.axaml`
  - 左侧导航按钮入口
  - 语言切换 `ComboBox`
  - 右侧 `ContentControl` 承载当前页面
- `ViewModels/MainWindowViewModel.cs`
  - 管理当前页面切换
  - 管理窗口标题、侧边栏标题
  - 接收配置变化并刷新 UI

## 2. 页面导航 Hooks

文件：`ViewModels/MainWindowViewModel.cs`

### 页面枚举

- `LauncherSection.Overview`
- `LauncherSection.Instances`
- `LauncherSection.Library`
- `LauncherSection.Backups`
- `LauncherSection.Customize`

### 导航命令

- `NavigateOverview`
- `NavigateInstances`
- `NavigateLibrary`
- `NavigateBackups`
- `NavigateCustomize`

### 导航按钮绑定

文件：`Views/MainWindow.axaml`

- `NavigateOverviewCommand`
- `NavigateInstancesCommand`
- `NavigateLibraryCommand`
- `NavigateBackupsCommand`
- `NavigateCustomizeCommand`

## 3. 语言 Hooks

### 语言服务

文件：`Services/LocalizationService.cs`

- 保存当前语言：`CurrentLanguage`
- 暴露语言列表：`AvailableLanguages`
- 语言切换事件：`LanguageChanged`
- 配置联动：
  - 修改 `CurrentLanguage`
  - 自动写入 `AppConfigService.CurrentLanguage`

### 主窗口语言入口

文件：`ViewModels/MainWindowViewModel.cs`

- `Languages`
- `SelectedLanguage`
- `OnSelectedLanguageChanged`
- `OnLanguageChanged`

文件：`Views/MainWindow.axaml`

- `ComboBox SelectedItem="{Binding SelectedLanguage}"`

### 页面级语言刷新基类

文件：`ViewModels/LocalizedViewModelBase.cs`

- 监听 `LocalizationService.LanguageChanged`
- 统一调用各页面自己的 `OnLanguageChanged()`
- 提供 `RaiseProperties(...)` 批量刷新属性

### 实际使用语言 hook 的页面

- `ViewModels/OverviewPageViewModel.cs`
- `ViewModels/InstancesPageViewModel.cs`
- `ViewModels/LibraryPageViewModel.cs`
- `ViewModels/BackupsPageViewModel.cs`
- `ViewModels/CustomizePageViewModel.cs`

这些页面都通过 `OnLanguageChanged()` 刷新文本。

## 4. 主题 / 密度 / 圆角 Hooks

### 主题服务

文件：`Services/ThemeCustomizationService.cs`

#### 状态

- `SelectedPreset`
- `SelectedDensity`
- `SelectedShape`

#### 事件

- `AppearanceChanged`

#### 命令触发后的实际动作

- `ApplyTheme()`
- `ApplyPalette(...)`
- `ApplyShape(...)`
- `ApplyDensity(...)`

#### 配置联动

- `SelectedPreset -> AppConfigService.ThemePreset`
- `SelectedDensity -> AppConfigService.Density`
- `SelectedShape -> AppConfigService.Shape`

### 实际改写的全局资源

文件：`App.axaml`

#### 颜色 / 画刷

- `PageBaseBrush`
- `SurfaceBrush`
- `SurfaceAltBrush`
- `SidebarBrush`
- `SidebarAltBrush`
- `AccentBrush`
- `AccentDeepBrush`
- `HighlightBrush`
- `InkBrush`
- `MutedInkBrush`
- `SidebarTextBrush`
- `SidebarMutedTextBrush`
- `HairlineBrush`
- `WarmOverlayBrush`
- `HeroBrush`
- `LaunchButtonBrush`

#### 圆角资源

- `SurfaceCardRadius`
- `SoftCardRadius`
- `ControlCornerRadius`
- `LaunchButtonRadius`

#### 间距资源

- `SecondaryButtonPadding`
- `NavButtonPadding`
- `LaunchButtonPadding`
- `CardInnerPadding`

## 5. 标题自定义 Hooks

### 配置源

文件：`Services/AppConfigService.cs`

- `LauncherTitle`

### 主窗口使用点

文件：`ViewModels/MainWindowViewModel.cs`

- `WindowTitle => configService.LauncherTitle`
- `SidebarTitle => configService.LauncherTitle`
- `HandleConfigPropertyChanged(...)`

### 自定义页使用点

文件：`ViewModels/CustomizePageViewModel.cs`

- `LauncherTitle`
- `HandleConfigPropertyChanged(...)`

文件：`Views/CustomizePageView.axaml`

- `TextBox Text="{Binding LauncherTitle}"`
- 预览卡片标题也绑定 `LauncherTitle`

## 6. 自定义页 Hooks

文件：`ViewModels/CustomizePageViewModel.cs`

### 主题切换命令

- `UseMossTheme`
- `UseMidnightTheme`
- `UseEmberTheme`

### 密度切换命令

- `UseComfortableDensity`
- `UseCompactDensity`

### 圆角风格切换命令

- `UseRoundedShape`
- `UseDefinedShape`

### 重置命令

- `ResetAppearance`

### 当前状态显示

- `IsMossSelected`
- `IsMidnightSelected`
- `IsEmberSelected`
- `IsComfortableSelected`
- `IsCompactSelected`
- `IsRoundedSelected`
- `IsDefinedSelected`
- `CurrentThemeName`
- `CurrentDensityName`
- `CurrentShapeName`
- `LauncherTitle`
- `ConfigPath`

### 事件联动

- 监听 `themeService.AppearanceChanged`
- 监听 `configService.PropertyChanged`

## 7. 配置文件 Hooks

文件：`Services/AppConfigService.cs`

### 路径

- 目录：`AppContext.BaseDirectory/AZL`
- 文件：`AppContext.BaseDirectory/AZL/config.ini`

### 当前写入项

```ini
[general]
language=...
launcher_title=...

[appearance]
theme_preset=...
density=...
shape=...
```

### 自动保存触发点

- 修改 `CurrentLanguage`
- 修改 `ThemePreset`
- 修改 `Density`
- 修改 `Shape`
- 修改 `LauncherTitle`

## 8. 视图文件对应关系

### 主框架

- `Views/MainWindow.axaml`
- `ViewModels/MainWindowViewModel.cs`

### 页面

- `Views/OverviewPageView.axaml`
- `ViewModels/OverviewPageViewModel.cs`

- `Views/InstancesPageView.axaml`
- `ViewModels/InstancesPageViewModel.cs`

- `Views/LibraryPageView.axaml`
- `ViewModels/LibraryPageViewModel.cs`

- `Views/BackupsPageView.axaml`
- `ViewModels/BackupsPageViewModel.cs`

- `Views/CustomizePageView.axaml`
- `ViewModels/CustomizePageViewModel.cs`

## 9. 如果后续要继续加 Hook，建议放这里

优先追加到以下位置：

- 新的全局 UI 状态：`Services/AppConfigService.cs`
- 新的主题/样式应用逻辑：`Services/ThemeCustomizationService.cs`
- 新的语言联动字段：对应页面的 `OnLanguageChanged()`
- 新的二级界面入口：`LauncherSection` + `Navigate...`
- 新的自定义控件入口：`CustomizePageViewModel.cs` + `CustomizePageView.axaml`

## 10. 当前项目里最核心的 UI Hook 文件

如果只看最关键的几个文件，优先看：

1. `Services/AppConfigService.cs`
2. `Services/LocalizationService.cs`
3. `Services/ThemeCustomizationService.cs`
4. `ViewModels/MainWindowViewModel.cs`
5. `ViewModels/CustomizePageViewModel.cs`
6. `Views/MainWindow.axaml`
7. `Views/CustomizePageView.axaml`
