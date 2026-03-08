# PreviewViewModel - Документация

## Обзор
`PreviewViewModel` - это ViewModel для управления превью видео в WPF Video Editor. Работает по паттерну MVVM и интегрирован с `TimelineViewModel`.

## Возможности

### 1. **Свойства**

#### `IsPlaying` (bool)
- Состояние воспроизведения (играет/пауза)
- Автоматически синхронизируется с `Timeline.IsPlaying`
- При изменении запускает/останавливает внутренний таймер

#### `CurrentTime` (TimeSpan)
- Текущее время воспроизведения
- **TwoWay биндинг** с `Timeline.PlayheadPosition`
- При изменении вызывает событие `PreviewFrameNeeded`

#### `PreviewFPS` (int, по умолчанию: 24)
- Частота кадров для превью
- Допустимый диапазон: 1-120 fps
- При изменении обновляет интервал таймера

#### `TotalDuration` (TimeSpan)
- Общая длительность таймлайна
- Вычисляется автоматически через `UpdateTotalDuration()`

### 2. **Команды**

#### `PlayPauseCommand`
- Переключает воспроизведение/паузу
- Биндится на кнопку Play/Pause

#### `NextFrameCommand`
- Перемещает на следующий кадр
- Шаг = 1/PreviewFPS секунды

#### `PreviousFrameCommand`
- Перемещает на предыдущий кадр
- Шаг = 1/PreviewFPS секунды

#### `SeekCommand`
- Перемещает на указанное время
- Параметр: `TimeSpan` или `double` (секунды)

### 3. **События**

#### `PreviewFrameNeeded`
```csharp
public event Action<TimeSpan> PreviewFrameNeeded;
```

Вызывается когда нужен новый кадр для отображения.

**Пример подписки:**
```csharp
viewModel.Preview.PreviewFrameNeeded += (time) => 
{
    var frame = _videoService.GetFrameAt(time);
    UpdatePreviewImage(frame);
};
```

## Интеграция в MainWindow.xaml

### Transport Controls (Play/Pause/Frame Navigation)
```xml
<StackPanel Orientation="Horizontal">
    <!-- Previous Frame -->
    <Button Command="{Binding Preview.PreviousFrameCommand}" 
            Content="⏮" Width="45" Height="35"/>
    
    <!-- Play/Pause with dynamic icon -->
    <Button Command="{Binding Preview.PlayPauseCommand}" 
            Width="45" Height="35">
        <Button.Style>
            <Style TargetType="Button">
                <Setter Property="Content" Value="▶"/>
                <Style.Triggers>
                    <DataTrigger Binding="{Binding Preview.IsPlaying}" Value="True">
                        <Setter Property="Content" Value="⏸"/>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </Button.Style>
    </Button>
    
    <!-- Next Frame -->
    <Button Command="{Binding Preview.NextFrameCommand}" 
            Content="⏭" Width="45" Height="35"/>
    
    <!-- Current Time Display -->
    <TextBlock Text="{Binding Preview.CurrentTime, StringFormat={}{0:hh\\:mm\\:ss\\.ff}}" 
               FontWeight="Bold"/>
    
    <!-- Timeline Slider (TwoWay binding) -->
    <Slider Minimum="0" 
            Maximum="{Binding Preview.TotalDuration.TotalSeconds}"
            Value="{Binding Preview.CurrentTime.TotalSeconds, Mode=TwoWay}"/>
    
    <!-- Total Duration Display -->
    <TextBlock Text="{Binding Preview.TotalDuration, StringFormat={}{0:hh\\:mm\\:ss}}"/>
</StackPanel>
```

## Синхронизация с Timeline

### Автоматическая двунаправленная синхронизация:
- **Timeline → Preview**: Когда пользователь перемещает playhead на таймлайне, `CurrentTime` автоматически обновляется
- **Preview → Timeline**: Когда меняется `CurrentTime`, `PlayheadPosition` на таймлайне обновляется

### Конвертация координат:
```csharp
// TimeSpan → Pixels
pixels = timeSpan.TotalSeconds * Timeline.TimelineScale

// Pixels → TimeSpan
timeSpan = TimeSpan.FromSeconds(pixels / Timeline.TimelineScale)
```

## Использование в MainViewModel

```csharp
public class MainViewModel : ViewModelBase
{
    public PreviewViewModel Preview { get; }
    
    public MainViewModel(
        TimelineViewModel timelineViewModel,
        PreviewViewModel previewViewModel)
    {
        Preview = previewViewModel;
        
        // Делегируем команды в Preview
        PlayPauseCommand = Preview.PlayPauseCommand;
        NextFrameCommand = Preview.NextFrameCommand;
        PreviousFrameCommand = Preview.PreviousFrameCommand;
    }
}
```

## Пример работы с VideoTimelinePreview

```csharp
// В MainWindow.xaml.cs или в коде где нужен рендеринг
private VideoTimelinePreview _videoPreview;

private void SetupPreview()
{
    _videoPreview = new VideoTimelinePreview();
    
    // Подписываемся на событие запроса кадра
    ViewModel.Preview.PreviewFrameNeeded += OnPreviewFrameNeeded;
    
    // Привязываем ImageSource к превью
    PreviewImage.Source = _videoPreview.PreviewImageSource;
}

private void OnPreviewFrameNeeded(TimeSpan time)
{
    // Запрашиваем кадр у сервиса
    _videoPreview.Seek(time.TotalSeconds);
}
```

## Методы

### `UpdateTotalDuration()`
Обновляет `TotalDuration` на основе клипов в Timeline:
```csharp
// Вызывать после добавления/удаления клипов
Preview.UpdateTotalDuration();
```

### `Reset()`
Сбрасывает воспроизведение:
```csharp
Preview.Reset(); // IsPlaying = false, CurrentTime = 0
```

## Архитектура

```
┌─────────────────┐
│  MainViewModel  │
│                 │
│  ┌───────────┐  │
│  │ Preview   │◄─┼──── Commands (PlayPause, NextFrame, etc.)
│  └─────┬─────┘  │
│        │        │
└────────┼────────┘
         │
         │ PreviewFrameNeeded
         ▼
┌────────────────────┐
│ VideoTimelinePreview│
│  (Rendering)        │
└────────────────────┘
         │
         │ ImageSource
         ▼
┌────────────────┐
│  MediaElement  │
│  or Image      │
└────────────────┘
```

## Timeline Workflow

1. **Пользователь нажимает Play**
   - `PlayPauseCommand` вызывается
   - `IsPlaying` = true
   - Таймер начинает работать с интервалом `1000/PreviewFPS` мс

2. **Каждый тик таймера**
   - Вычисляется прошедшее время
   - `CurrentTime` увеличивается
   - Вызывается `PreviewFrameNeeded(CurrentTime)`
   - `Timeline.PlayheadPosition` обновляется

3. **Пользователь двигает слайдер**
   - `TimelineSlider.Value` меняется
   - `Preview.CurrentTime.TotalSeconds` обновляется (TwoWay)
   - `Timeline.PlayheadPosition` синхронизируется
   - Вызывается `PreviewFrameNeeded(newTime)`

## Производительность

- **FPS**: Контролируйте через `PreviewFPS` (по умолчанию 24)
- **Таймер**: Использует `DispatcherPriority.Render` для плавности
- **Кадры**: Запрашиваются только при необходимости (событие)

## Тестирование

```csharp
[Test]
public void PlayPause_ToggleIsPlaying()
{
    var timeline = new TimelineViewModel(new ClipFactory());
    var preview = new PreviewViewModel(timeline);
    
    Assert.False(preview.IsPlaying);
    
    preview.PlayPauseCommand.Execute(null);
    Assert.True(preview.IsPlaying);
    
    preview.PlayPauseCommand.Execute(null);
    Assert.False(preview.IsPlaying);
}

[Test]
public void NextFrame_IncreasesCurrentTime()
{
    var preview = new PreviewViewModel(timeline);
    preview.PreviewFPS = 24;
    
    var initialTime = preview.CurrentTime;
    preview.NextFrameCommand.Execute(null);
    
    var expected = TimeSpan.FromSeconds(1.0 / 24);
    Assert.Equal(expected, preview.CurrentTime - initialTime);
}
```

## Troubleshooting

### Проблема: CurrentTime не синхронизируется с Timeline
**Решение**: Убедитесь что `Mode=TwoWay` в биндинге Slider

### Проблема: Play не работает
**Решение**: Проверьте что команды делегированы из MainViewModel:
```csharp
PlayPauseCommand = Preview.PlayPauseCommand;
```

### Проблема: Кадры не обновляются
**Решение**: Подпишитесь на событие `PreviewFrameNeeded`
