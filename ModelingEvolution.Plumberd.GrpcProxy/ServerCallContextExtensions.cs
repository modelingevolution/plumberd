﻿using System;
using System.Linq;
using System.Security.Claims;
using Grpc.Core;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    public static class ServerCallContextExtensions
    {
        public static Guid? SessionId(this ServerCallContext context)
        {
            
            var sessionEntry = context.RequestHeaders?.Get("sessionid-bin");
            if (sessionEntry != null && sessionEntry.IsBinary)
                return new Guid(sessionEntry.ValueBytes);
            return null;
        }
        public static Guid? UserId(this ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();

            var user = httpContext?.User;
            if (user == null) 
                return null;

            var userId = user.Claims
                .Where(x => x.Type == ClaimTypes.NameIdentifier || x.Type == "sub")
                .Select(x => x.Value)
                .FirstOrDefault();

            return userId != null ? Guid.TryParse(userId, out var ret) ? ret : null : null;
        }
        public static string UserName(this ServerCallContext context)
        {
            var httpContext = context?.GetHttpContext();
            var user = httpContext.User;
            return user?.Identity?.Name;
        }
        public static string UserEmail(this ServerCallContext context)
        {
            var httpContext = context?.GetHttpContext();
            var user = httpContext.User;
            return user?.Claims.Where(x=>x.Type == ClaimTypes.Email || x.Type == "email")
                .Select(x=>x.Value)
                .FirstOrDefault();
        }

        public static bool IsAuthenticated(this ServerCallContext context)
        {
            var httpContext = context.GetHttpContext();
            return httpContext?.User?.Identity?.IsAuthenticated ?? false;
        }
    }
}