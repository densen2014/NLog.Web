﻿using System;
using System.Linq;
using System.Text;

using NLog.Common;
using NLog.Config;
using NLog.LayoutRenderers;
#if ASP_NET_CORE
using Microsoft.AspNetCore.Http;
using HttpContextBase = Microsoft.AspNetCore.Http.HttpContext;
using Microsoft.Extensions.DependencyInjection;
using NLog.Web.DependencyInjection;
#else
using System.Web;
#endif

namespace NLog.Web.LayoutRenderers
{
    /// <summary>
    /// Base class for ASP.NET layout renderers.
    /// </summary>
    public abstract class AspNetLayoutRendererBase : LayoutRenderer
    {
        /// <summary>
        /// Context for DI
        /// </summary>
        private IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Provides access to the current request HttpContext.
        /// </summary>
        /// <returns>HttpContextAccessor or <c>null</c></returns>
        [NLogConfigurationIgnoreProperty]
        public IHttpContextAccessor HttpContextAccessor
        {
            get => _httpContextAccessor ?? (_httpContextAccessor = RetrieveHttpContextAccessor(GetType()));
            set => _httpContextAccessor = value;
        }

#if !ASP_NET_CORE
        internal static IHttpContextAccessor DefaultHttpContextAccessor { get; set; } = new DefaultHttpContextAccessor();
        internal static IHttpContextAccessor RetrieveHttpContextAccessor(Type _) => DefaultHttpContextAccessor;
#else

        internal static IHttpContextAccessor RetrieveHttpContextAccessor(Type classType)
        {
            var serviceProvider = ServiceLocator.ServiceProvider;
            if (serviceProvider == null)
            {
                InternalLogger.Debug("{0} - No available HttpContext, because ServiceProvider is not registered", classType);
                return null;
            }

            try
            {
                var httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();
                if (httpContextAccessor == null)
                {
                    InternalLogger.Debug("{0} - No available HttpContext, because IHttpContextAccessor is not registered", classType);
                }

                return httpContextAccessor;
            }
            catch (ObjectDisposedException ex)
            {
                InternalLogger.Debug(ex, "{0} - No available HttpContext, because ServiceProvider has been disposed", classType);
                return null;
            }
        }
#endif

        /// <summary>
        /// Validates that the HttpContext is available and delegates append to subclasses.<see cref="StringBuilder" />.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder" /> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        {
            var httpContextAccessor = HttpContextAccessor;
            if (httpContextAccessor == null)
            {
                return;
            }

            if (httpContextAccessor.HttpContext == null)
            {
                InternalLogger.Debug("No available HttpContext, because outside valid request context. Logger: {0}", logEvent.LoggerName);
                return;
            }

            DoAppend(builder, logEvent);
        }

        /// <summary>
        /// Implemented by subclasses to render request information and append it to the specified <see cref="StringBuilder" />.
        /// 
        /// Won't be called if <see cref="HttpContextAccessor" /> of <see cref="IHttpContextAccessor.HttpContext" /> is <c>null</c>.
        /// </summary>
        /// <param name="builder">The <see cref="StringBuilder" /> to append the rendered data to.</param>
        /// <param name="logEvent">Logging event.</param>
        protected abstract void DoAppend(StringBuilder builder, LogEventInfo logEvent);

        /// <inheritdoc />
        protected override void CloseLayoutRenderer()
        {
            _httpContextAccessor = null;
            base.CloseLayoutRenderer();
        }

        /// <summary>
        /// Register a custom layout renderer with a callback function <paramref name="func" />. The callback receives the logEvent and the current configuration.
        /// </summary>
        /// <param name="name">Name of the layout renderer - without ${}.</param>
        /// <param name="func">Callback that returns the value for the layout renderer.</param>
        public static void Register(string name, Func<LogEventInfo, HttpContextBase, LoggingConfiguration, object> func)
        {
            var renderer = new NLogWebFuncLayoutRenderer(name, func);
            Register(renderer);
        }
    }
}

