﻿using System;
using System.Net;
using Cofoundry.Core.AutoUpdate;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cofoundry.Web
{
    /// <summary>
    /// Runs the auto-updater module during the first request and locks out other requests
    /// until the update is complete.
    /// </summary>
    public class AutoUpdateMiddleware
    {
        private enum UpdateStatus
        {
            NotStarted,
            InProgress,
            Complete
        }

        private static UpdateStatus _updateStatus = UpdateStatus.NotStarted;
        private static object _updateStatusLock = new object();

        private readonly RequestDelegate _next;

        public AutoUpdateMiddleware(
            RequestDelegate next
            ) 
        {
            _next = next;
        }

        public async Task Invoke(HttpContext cx)
        {
            bool runUpdate = false;

            if (_updateStatus == UpdateStatus.NotStarted)
            {
                lock (_updateStatusLock)
                {
                    if (_updateStatus == UpdateStatus.NotStarted)
                    {
                        _updateStatus = UpdateStatus.InProgress;
                        runUpdate = true;
                    }
                }

                if (runUpdate)
                {
                    try
                    {
                        var autoUpdateService = cx.RequestServices.GetService<IAutoUpdateService>();
                        await autoUpdateService.UpdateAsync();
                        _updateStatus = UpdateStatus.Complete;
                    }
                    catch (AutoUpdateProcessLockedException lockedException)
                    {
                        _updateStatus = UpdateStatus.NotStarted;
                        await cx.Response.WriteAsync("The application is being updated and is currently locked. Please try again shortly.");
                    }
                    catch (Exception ex)
                    {
                        _updateStatus = UpdateStatus.NotStarted;
                        throw;
                    }
                }
            }

            if (_updateStatus == UpdateStatus.InProgress)
            {
                cx.Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                await cx.Response.WriteAsync("The application is being updated. Please try again shortly.");
            }
            else if (_updateStatus == UpdateStatus.Complete)
            {
                await _next.Invoke(cx);
            }
        }
    }
}