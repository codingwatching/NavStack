#if NAVSTACK_UGUI_SUPPORT
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using NavStack.Internal;

namespace NavStack.UI
{
    [AddComponentMenu("NavStack/Navigation Stack UI")]
    [RequireComponent(typeof(RectTransform))]
    public class NavigationStackUI : MonoBehaviour, INavigationStack
    {
        [SerializeField] RectTransform parentTransform;

        readonly NavigationStackCore core = new();

        public event Action<IPage> OnPageAttached
        {
            add => core.OnPageAttached += value;
            remove => core.OnPageAttached -= value;
        }

        public event Action<IPage> OnPageDetached
        {
            add => core.OnPageDetached += value;
            remove => core.OnPageDetached -= value;
        }

        public event Action<(IPage Previous, IPage Current)> OnNavigated
        {
            add => core.OnNavigated += value;
            remove => core.OnNavigated -= value;
        }

        public event Action<(IPage Previous, IPage Current)> OnNavigating
        {
            add => core.OnNavigating += value;
            remove => core.OnNavigating -= value;
        }

        public IPage ActivePage => core.ActivePage;
        public IReadOnlyCollection<IPage> Pages => core.Pages;

        protected virtual void Awake()
        {
            OnPageAttached += page =>
            {
                if (page is Component component)
                {
                    if (parentTransform != null)
                    {
                        component.transform.SetParent(parentTransform, false);
                    }
                }
            };
        }

        public UniTask PopAsync(NavigationContext context, CancellationToken cancellationToken = default)
        {
            return core.PopAsync(context, cancellationToken);
        }

        public UniTask PushAsync(IPage page, NavigationContext context, CancellationToken cancellationToken = default)
        {
            return core.PushAsync(() => new(page), context, cancellationToken);
        }

        public UniTask PushAsync(Func<UniTask<IPage>> factory, NavigationContext context, CancellationToken cancellationToken = default)
        {
            return core.PushAsync(factory, context, cancellationToken);
        }
    }
}
#endif