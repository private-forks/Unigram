﻿using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Geometry;
using Microsoft.Graphics.Canvas.UI.Xaml;
using RLottie;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Native;
using Unigram.Services;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.System.Threading;
using Windows.UI;

namespace Unigram.Controls.Messages
{
    public class CustomEmojiCanvas : AnimatedControl<object>
    {
        private readonly HashSet<long> _cache = new();
        private readonly List<EmojiPosition> _positions = new();

        private int _emojiSize;

        private bool _dropState;

        public CustomEmojiCanvas()
            : base(true)
        {
            DefaultStyleKey = typeof(CustomEmojiCanvas);
            AutoPlay = false;

            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
            _emojiSize = GetDpiAwareSize(20);
        }

        protected override CanvasBitmap CreateBitmap(CanvasDevice device)
        {
            var awareSize = GetDpiAwareSize(_currentSize.X, _currentSize.Y);

            bool needsCreate = _bitmap == null;
            needsCreate |= _bitmap?.Size.Width != awareSize.Width || _bitmap?.Size.Height != awareSize.Height;
            needsCreate |= _bitmap?.Device != device;
            needsCreate &= _currentSize.X > 0 && _currentSize.Y > 0;

            if (needsCreate)
            {
                _bitmap?.Dispose();
                _bitmap = null;

                _emojiSize = GetDpiAwareSize(20);
                return CreateBitmap(device, awareSize.Width, awareSize.Height);
            }

            return _bitmap;
        }

        protected override void Dispose()
        {
            var hash = GetHashCode();

            foreach (var customEmojiId in _cache)
            {
                EmojiRendererCache.Release(customEmojiId, hash);
            }

            _cache.Clear();
        }

        protected override void DrawFrame(CanvasImageSource sender, CanvasDrawingSession args)
        {
            ICanvasBrush brush = null;
            ISet<long> outdated = null;

            if (_dropState)
            {
                var buffer = ArrayPool<byte>.Shared.Rent((int)(_bitmap.SizeInPixels.Width * _bitmap.SizeInPixels.Height * 4));

                _dropState = false;
                _bitmap.SetPixelBytes(buffer);

                ArrayPool<byte>.Shared.Return(buffer);
            }

            foreach (var item in _positions)
            {
                if (EmojiRendererCache.TryGet(item.CustomEmojiId, out EmojiRenderer animation))
                {
                    var x = (int)((2 + item.X - 0) * (_currentDpi / 96));
                    var y = (int)((2 + item.Y - 0) * (_currentDpi / 96));

                    var matches = _emojiSize * _emojiSize * 4 == animation.Buffer?.Length;
                    if (matches && animation.HasRenderedFirstFrame && x + _emojiSize <= _bitmap.Size.Width && y + _emojiSize <= _bitmap.Size.Height)
                    {
                        _bitmap.SetPixelBytes(animation.Buffer, x, y, _emojiSize, _emojiSize);
                        animation.CloseOutline();
                    }
                    else
                    {
                        args.FillGeometry(animation.GetOutline(sender), item.X, item.Y, brush ??= new CanvasSolidColorBrush(sender, Color.FromArgb(0x33, 0x7A, 0x8A, 0x96)));

                        if (_subscribed && !matches && !animation.IsLoading)
                        {
                            outdated ??= new HashSet<long>();
                            outdated.Add(item.CustomEmojiId);
                        }
                    }
                }
            }

            args.DrawImage(_bitmap, new Rect(0, 0, sender.Size.Width, sender.Size.Height));

            if (brush != null)
            {
                brush.Dispose();
            }

            if (outdated != null)
            {
                EmojiRendererCache.Reload(outdated, _emojiSize);
            }
        }

        protected override void NextFrame()
        {
            //foreach (var item in _cache.Values)
            //{
            //    item.NextFrame();
            //}
        }

        protected override void OnSubscribe(bool subscribe)
        {
            if (subscribe == _subscribed)
            {
                return;
            }

            var hash = GetHashCode();

            foreach (var item in _cache)
            {
                if (subscribe)
                {
                    EmojiRendererCache.Show(item, hash);
                }
                else
                {
                    EmojiRendererCache.Hide(item, hash);
                }
            }
        }

        protected override void SourceChanged()
        {
            OnSourceChanged();
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            _layoutRoot?.Measure(new Size(availableSize.Width, availableSize.Height));
            _canvas?.Measure(new Size(availableSize.Width, availableSize.Height));

            return new Size(0, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            _layoutRoot?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            _canvas?.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));

            if (_canvas != null)
            {
                _canvas.MaxWidth = finalSize.Width;
                _canvas.MaxHeight = finalSize.Height;
            }

            return finalSize;
        }

        public void UpdatePositions(IList<EmojiPosition> positions)
        {
            _positions.Clear();
            _positions.AddRange(positions);

            _dropState = true;
        }

        public async void UpdateEntities(IClientService clientService, ISet<long> customEmoji)
        {
            //if (_loadEmojiToken != null)
            //{
            //    _loadEmojiToken.Cancel();
            //}

            //var tsc = _loadEmoji = new TaskCompletionSource<bool>();
            //var token = _loadEmojiToken = new CancellationTokenSource();

            _animation = new object();

            var request = new List<long>();
            var hash = GetHashCode();

            foreach (var emoji in customEmoji)
            {
                if (_cache.Contains(emoji))
                {
                    continue;
                }

                if (EmojiRendererCache.TryMerge(emoji, hash, _subscribed, out _))
                {
                    _cache.Add(emoji);
                    continue;
                }

                request.Add(emoji);
            }

            if (request.Count < 1)
            {
                OnSourceChanged();
                return;
            }

            var response = await clientService.SendAsync(new GetCustomEmojiStickers(request));
            if (response is Stickers stickers)
            {
                //if (token.IsCancellationRequested)
                //{
                //    return;
                //}

                foreach (var sticker in stickers.StickersValue)
                {
                    if (_cache.Contains(sticker.CustomEmojiId))
                    {
                        continue;
                    }

                    _cache.Add(sticker.CustomEmojiId);
                    EmojiRendererCache.MergeOrCreate(clientService, sticker, _emojiSize, hash, _subscribed);
                }
            }

            OnSourceChanged();
        }
    }

    public static class EmojiRendererCache
    {
        private static readonly ConcurrentDictionary<long, EmojiRenderer> _cache = new();

        private static TimeSpan _interval;

        private static readonly object _lock = new();

        static EmojiRendererCache()
        {
            _interval = TimeSpan.FromMilliseconds(Math.Floor(1000d / 30));
        }

        public static bool Contains(long customEmojiId)
        {
            return _cache.ContainsKey(customEmojiId);
        }

        public static bool TryGet(long customEmojiId, out EmojiRenderer renderer)
        {
            return _cache.TryGetValue(customEmojiId, out renderer);
        }

        public static bool TryGetValue(long customEmojiId, out Sticker sticker)
        {
            if (_cache.TryGetValue(customEmojiId, out EmojiRenderer renderer))
            {
                sticker = renderer.Sticker;
                return true;
            }

            sticker = null;
            return false;
        }

        public static bool TryMerge(long customEmojiId, int hash, bool subscribe, out EmojiRenderer renderer)
        {
            if (_cache.TryGetValue(customEmojiId, out renderer))
            {
                if (subscribe)
                {
                    Show(renderer, hash);
                }

                lock (_lock)
                {
                    renderer.References.Add(hash);
                }

                return true;
            }

            return false;
        }

        public static void MergeOrCreate(IClientService clientService, Sticker sticker, int size, int hash, bool subscribe)
        {
            if (TryMerge(sticker.CustomEmojiId, hash, subscribe, out EmojiRenderer renderer))
            {
                return;
            }

            _cache[sticker.CustomEmojiId] = renderer = new EmojiRenderer(clientService, sticker, size);

            if (subscribe)
            {
                Show(renderer, hash);
            }

            lock (_lock)
            {
                renderer.References.Add(hash);
            }
        }

        public static void Release(long customEmojiId, int hash)
        {
            if (_cache.TryGetValue(customEmojiId, out EmojiRenderer reference))
            {
                lock (_lock)
                {
                    reference.References.Remove(hash);
                    reference.HashCodes.Remove(hash);
                }

                if (reference.IsAlive is false)
                {
                    _cache.TryRemove(customEmojiId, out _);
                    reference.Dispose();
                }
            }
        }

        public static void Show(long customEmojiId, int hash)
        {
            if (TryGet(customEmojiId, out EmojiRenderer renderer))
            {
                Show(renderer, hash);
            }
        }

        public static void Show(EmojiRenderer renderer, int hash)
        {
            lock (_lock)
            {
                renderer.HashCodes.Add(hash);

                if (renderer.IsAnimated && renderer.IsVisible)
                {
                    renderer.Subscribe(true);
                }
            }
        }

        public static void Hide(long customEmojiId, int hash)
        {
            if (TryGet(customEmojiId, out EmojiRenderer renderer))
            {
                Hide(renderer, hash);
            }
        }

        public static void Hide(EmojiRenderer renderer, int hash)
        {
            lock (_lock)
            {
                renderer.HashCodes.Remove(hash);

                if (renderer.IsAnimated && !renderer.IsVisible)
                {
                    renderer.Subscribe(false);
                }
            }
        }

        private static void NextFrame()
        {
            foreach (var item in _cache.Values)
            {
                if (item.IsVisible)
                {
                    item.NextFrame();
                }
            }
        }

        public static void Reload(ISet<long> emoji, int size)
        {
            List<EmojiRenderer> renderers = null;

            foreach (var customEmojiId in emoji)
            {
                if (_cache.TryGetValue(customEmojiId, out EmojiRenderer renderer))
                {
                    if (renderer.Size != size)
                    {
                        var clone = renderer.Clone(size);
                        renderer.Dispose();

                        _cache[customEmojiId] = clone;
                        clone.IsLoading = true;

                        renderers ??= new List<EmojiRenderer>();
                        renderers.Add(clone);
                    }
                    else if (renderer.Buffer == null && !renderer.IsLoading)
                    {
                        renderer.IsLoading = true;

                        renderers ??= new List<EmojiRenderer>();
                        renderers.Add(renderer);
                    }
                }
            }

            _ = Windows.System.Threading.ThreadPool.RunAsync(async _ =>
            {
                foreach (var item in renderers)
                {
                    if (item.Buffer == null)
                    {
                        await item.CreateAsync();
                    }
                }
            }, WorkItemPriority.Low);
        }
    }

    public class EmojiRenderer
    {
        private readonly IClientService _clientService;
        private readonly Sticker _sticker;

        private object _animation;
        private IBuffer _buffer;

        private readonly int _size;

        private int _animationTotalFrame;
        private double _animationFrameRate;

        private readonly bool _limitFps = true;

        private TimeSpan _interval;
        private LoopThread _timer;
        private bool _subscribed;

        private static readonly SemaphoreSlim _nextFrameLock = new(1, 1);

        private int _index;
        private double _step;

        private int _loopCount;

        public EmojiRenderer(IClientService clientService, Sticker sticker, int size)
        {
            _clientService = clientService;
            _sticker = sticker;
            _size = size;
        }

        public EmojiRenderer Clone(int size)
        {
            return new EmojiRenderer(_clientService, _sticker, size);
        }

        public Sticker Sticker => _sticker;

        public long CustomEmojiId => _sticker.CustomEmojiId;

        public IBuffer Buffer => _buffer;

        public int Size => _size;

        public HashSet<int> References { get; } = new();
        public HashSet<int> HashCodes { get; } = new();

        public bool IsAlive => References.Count > 0;
        public bool IsVisible => HashCodes.Count > 0;

        public bool IsAnimated => _sticker.Format is not StickerFormatWebp;

        public bool IsLoading { get; set; }

        private CanvasGeometry _outline;
        public CanvasGeometry GetOutline(ICanvasResourceCreator sender)
        {
            if (_outline == null || _outline.Device != sender.Device)
            {
                var scale = 20f / _sticker.Width;
                var outline = CompositionPathParser.Parse(sender, _sticker.Outline, scale);

                _outline = outline;
            }

            return _outline;
        }

        public void CloseOutline()
        {
            if (_outline != null)
            {
                _outline.Dispose();
                _outline = null;
            }
        }

        private bool _hasRenderedFirstFrame;
        public bool HasRenderedFirstFrame => _hasRenderedFirstFrame && _buffer != null;

        public int LoopCount => _loopCount;

        public async Task CreateAsync()
        {
            var file = _sticker.StickerValue;
            if (file.Local.CanBeDownloaded && !file.Local.IsDownloadingCompleted)
            {
                file = await _clientService.DownloadFileAsync(file, 32);
            }

            // IsDownloadingCompleted was updated either
            // by IsFileExisting or by DownloadFileAsync.
            if (file.Local.IsDownloadingCompleted)
            {
                if (_sticker.Format is StickerFormatTgs)
                {
                    var animation = LottieAnimation.LoadFromFile(file.Local.Path, new Windows.Graphics.SizeInt32 { Width = _size, Height = _size }, true, GetColorReplacements(_sticker.SetId));
                    if (animation != null)
                    {
                        var frameRate = Math.Clamp(animation.FrameRate, 30, 30);

                        _interval = TimeSpan.FromMilliseconds(Math.Floor(1000 / frameRate));

                        _animationTotalFrame = animation.TotalFrame;
                        _animationFrameRate = animation.FrameRate;

                        _buffer = BufferSurface.Create((uint)(_size * _size * 4));
                        _animation = animation;

                        Subscribe(_subscribed);
                    }
                }
                else if (_sticker.Format is StickerFormatWebm)
                {
                    var animation = CachedVideoAnimation.LoadFromFile(new LocalVideoSource(file), _size, _size, true);
                    if (animation != null)
                    {
                        var frameRate = Math.Clamp(animation.FrameRate, 1, 30);

                        _interval = TimeSpan.FromMilliseconds(1000d / frameRate);

                        _animationFrameRate = animation.FrameRate;

                        _buffer = BufferSurface.Create((uint)(_size * _size * 4));
                        _animation = animation;

                        Subscribe(_subscribed);
                    }
                }
                else if (_sticker.Format is StickerFormatWebp)
                {
                    _buffer = PlaceholderImageHelper.Current.DrawWebP(file.Local.Path, _size, out _);
                    _hasRenderedFirstFrame = true;
                }
            }
        }

        public void NextFrame()
        {
            if (_animation is LottieAnimation lottie)
            {
                NextFrame(lottie);
            }
            else if (_animation is CachedVideoAnimation cached)
            {
                NextFrame(cached);
            }
        }

        private void NextFrame(LottieAnimation animation)
        {
            if (animation == null || animation.IsCaching || _buffer == null /*|| _unloaded*/)
            {
                return;
            }

            var index = _index;
            var framesPerUpdate = _limitFps ? _animationFrameRate < 60 ? 1 : 2 : 1;

            animation.RenderSync(_buffer, _size, _size, index);

            if (index + framesPerUpdate < _animationTotalFrame)
            {
                _index += framesPerUpdate;
            }
            else
            {
                _index = 0;

                var count = Interlocked.CompareExchange(ref _loopCount, 0, 1);
                if (count == 0)
                {
                    Interlocked.Exchange(ref _loopCount, 1);
                }
            }

            _hasRenderedFirstFrame = true;
        }

        private void NextFrame(CachedVideoAnimation animation)
        {
            if (animation == null || animation.IsCaching || _buffer == null /*|| _unloaded*/)
            {
                return;
            }

            animation.RenderSync(_buffer, _size, _size, out _, out bool completed);

            if (completed)
            {
                var count = Interlocked.CompareExchange(ref _loopCount, 0, 1);
                if (count == 0)
                {
                    Interlocked.Exchange(ref _loopCount, 1);
                }
            }

            _hasRenderedFirstFrame = true;
        }

        private Color _currentReplacement = Colors.Black;
        private readonly Dictionary<int, int> _colorReplacements = new()
        {
            { 0xFFFFFF, 0x000000 }
        };

        private IReadOnlyDictionary<int, int> GetColorReplacements(long setId)
        {
            if (setId == _clientService.Options.ThemedEmojiStatusesStickerSetId)
            {
                if (_currentReplacement != Theme.Accent)
                {
                    _currentReplacement = Theme.Accent;
                    _colorReplacements[0xFFFFFF] = Theme.Accent.ToValue();
                }

                return _colorReplacements;
            }

            return null;
        }

        public void Dispose()
        {
            if (_animation is LottieAnimation lottie && !lottie.IsCaching)
            {
                lottie.Dispose();
            }

            _buffer = null;
            Subscribe(false);
        }

        public void Subscribe(bool subscribe)
        {
            if (subscribe)
            {
                if (_timer == null && _interval != TimeSpan.Zero)
                {
                    _timer = LoopThreadPool.Get(_interval);
                    _timer.Tick += PrepareNextFrame;
                }
            }
            else if (_timer != null)
            {
                _timer.Tick -= PrepareNextFrame;
                _timer = null;
            }

            _subscribed = subscribe;
        }

        private void PrepareNextFrame(object sender, EventArgs e)
        {
            if (_nextFrameLock.Wait(0))
            {
                NextFrame();
                _nextFrameLock.Release();
            }
        }
    }

    public struct EmojiPosition
    {
        public long CustomEmojiId { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }
}
