using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NavStack.Internal
{
    public sealed class NavigationSheetCore
    {
        public IPage ActivePage => activePage;
        IPage activePage;

        public IReadOnlyCollection<IPage> Pages => pages;
        readonly List<IPage> pages = new();

        public event Action<IPage> OnPageAttached;
        public event Action<IPage> OnPageDetached;
        public event Action<(IPage Previous, IPage Current)> OnNavigating;
        public event Action<(IPage Previous, IPage Current)> OnNavigated;

        bool isRunning;

        public async UniTask AddAsync(IPage page, CancellationToken cancellationToken = default)
        {
            OnPageAttached?.Invoke(page);
            if (page is IPageLifecycleEvent lifecycle)
            {
                await lifecycle.OnAttached(cancellationToken);
            }

            pages.Add(page);
        }

        public async UniTask RemoveAsync(IPage page, CancellationToken cancellationToken = default)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));

            var remove = pages.Remove(page);
            if (!remove) throw new InvalidOperationException(); // TODO: add message

            OnPageDetached?.Invoke(page);
            if (page is IPageLifecycleEvent lifecycle)
            {
                await lifecycle.OnDetached(cancellationToken);
            }
        }

        public UniTask RemoveAllAsync(CancellationToken cancellationToken = default)
        {
            var array = new UniTask[pages.Count];
            for (int i = 0; i < pages.Count; i++)
            {
                var page = pages[i];
                OnPageDetached?.Invoke(page); // TODO: fix callback timing
                array[i] = page is IPageLifecycleEvent lifecycle ? lifecycle.OnDetached(cancellationToken) : UniTask.CompletedTask;
            }

            activePage = null;
            pages.Clear();

            return UniTask.WhenAll(array);
        }

        public async UniTask ShowAsync(int index, NavigationContext context, CancellationToken cancellationToken = default)
        {
            var copiedContext = context with { };

            if (isRunning)
            {
                switch (copiedContext.AwaitOperation)
                {
                    case NavigationAwaitOperation.Sequential:
                        await UniTask.WaitWhile(() => isRunning, cancellationToken: cancellationToken);
                        break;
                    case NavigationAwaitOperation.Drop:
                        return;
                    case NavigationAwaitOperation.Error:
                        throw new InvalidOperationException("Navigation is currently in transition.");
                }
            }

            isRunning = true;

            try
            {
                var page = pages[index];
                if (activePage == page) return;

                var prevPage = activePage;
                activePage = page;

                var task1 = prevPage == null ? UniTask.CompletedTask : prevPage.OnNavigatedFrom(copiedContext, cancellationToken);
                var task2 = activePage.OnNavigatedTo(copiedContext, cancellationToken);

                OnNavigating?.Invoke((prevPage, activePage));

                await UniTask.WhenAll(task1, task2);

                OnNavigated?.Invoke((prevPage, activePage));
            }
            finally
            {
                isRunning = false;
            }
        }

        public async UniTask HideAsync(NavigationContext context, CancellationToken cancellationToken = default)
        {
            var copiedContext = context with { };

            if (activePage == null)
            {
                throw new InvalidOperationException(); // TODO: add message
            }

            if (isRunning)
            {
                switch (copiedContext.AwaitOperation)
                {
                    case NavigationAwaitOperation.Sequential:
                        await UniTask.WaitWhile(() => isRunning, cancellationToken: cancellationToken);
                        break;
                    case NavigationAwaitOperation.Drop:
                        return;
                    case NavigationAwaitOperation.Error:
                        throw new InvalidOperationException("Navigation is currently in transition.");
                }
            }

            isRunning = true;

            try
            {
                var prevPage = activePage;
                activePage = null;

                if (prevPage != null)
                {
                    OnNavigating?.Invoke((null, prevPage));
                    await prevPage.OnNavigatedFrom(copiedContext, cancellationToken);
                    OnNavigated?.Invoke((null, prevPage));
                }
            }
            finally
            {
                isRunning = false;
            }
        }
    }
}