using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using NavStack.Content;

namespace NavStack
{
    public static class NavigationStackExtensions
    {
        public static UniTask PushAsync(this INavigationStack navigationStack, IPage page, CancellationToken cancellationToken = default)
        {
            return navigationStack.PushAsync(page, new NavigationContext(), cancellationToken);
        }

        public static UniTask PushAsync(this INavigationStack navigationStack, Func<UniTask<IPage>> factory, CancellationToken cancellationToken = default)
        {
            return navigationStack.PushAsync(factory, new NavigationContext(), cancellationToken);
        }

        public static UniTask PushNewObjectAsync<T>(this INavigationStack navigationStack, T prefab, NavigationContext context, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object, IPage
        {
            return navigationStack.PushAsync(() =>
            {
                var instance = UnityEngine.Object.Instantiate(prefab);
                if (instance is Component component)
                {
                    component.GetCancellationTokenOnDestroy().RegisterWithoutCaptureExecutionContext(instance =>
                    {
                        if (instance != null) UnityEngine.Object.Destroy((UnityEngine.Object)instance);
                    }, instance);
                }
                return new(instance);
            }, context, cancellationToken);
        }

        public static UniTask PushNewObjectAsync<T>(this INavigationStack navigationStack, T prefab, CancellationToken cancellationToken = default)
            where T : UnityEngine.Object, IPage
        {
            return PushNewObjectAsync(navigationStack, prefab, new NavigationContext(), cancellationToken);
        }

        public static UniTask PushNewObjectAsync(this INavigationStack navigationStack, string key, CancellationToken cancellationToken = default)
        {
            return PushNewObjectAsync(navigationStack, key, new NavigationContext(), cancellationToken: cancellationToken);
        }

        public static UniTask PushNewObjectAsync(this INavigationStack navigationStack, string key, NavigationContext context, IResourceProvider resourceProvider = null, bool loadAsync = true, CancellationToken cancellationToken = default)
        {
            return navigationStack.PushAsync(async () =>
            {
                resourceProvider ??= ResourceProvider.DefaultResourceProvider;

                UnityEngine.Object resource;

                if (loadAsync)
                {
                    resource = await resourceProvider.LoadAsync<UnityEngine.Object>(key, cancellationToken);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    resource = resourceProvider.Load<UnityEngine.Object>(key);
                }

                var instance = UnityEngine.Object.Instantiate(resource);
                if (!TryGetComponent<IPage>(instance, out var page)) throw new Exception(); // TODO:

                void OnPageDetached(IPage pageDetached)
                {
                    if (pageDetached != page) return;
                    if (instance != null) UnityEngine.Object.Destroy(instance);
                    resourceProvider.UnloadAsync(resource).Forget();
                    navigationStack.OnPageDetached -= OnPageDetached;
                }

                navigationStack.OnPageDetached += OnPageDetached;

                return page;
            }, context, cancellationToken);
        }

        public static UniTask PopAsync(this INavigationStack navigationStack, CancellationToken cancellationToken = default)
        {
            return navigationStack.PopAsync(new NavigationContext(), cancellationToken);
        }

        static bool TryGetComponent<T>(UnityEngine.Object obj, out T result)
        {
            if (obj is GameObject gameObject)
            {
                result = gameObject.GetComponent<T>();
                return true;
            }

            if (obj is Component component)
            {
                result = component.GetComponent<T>();
                return true;
            }

            result = default;
            return false;
        }
    }
}