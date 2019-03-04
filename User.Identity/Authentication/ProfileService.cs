using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using IdentityServer4.Models;
using IdentityServer4.Services;

namespace User.Identity.Authentication
{
    public class ProfileService:IProfileService
    {
        public async Task GetProfileDataAsync(ProfileDataRequestContext context)
        {
            var subject = context.Subject ?? throw new ArgumentNullException(nameof(context.Subject));
            if (context.Subject.Claims.Any())
            {
                context.IssuedClaims = context.Subject.Claims.ToList();
            }
            await Task.CompletedTask;


        }

        public async Task IsActiveAsync(IsActiveContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(context.Subject?.GetSubjectId()))
            {
                context.IsActive = false;
            }
            context.IsActive = true;
            await Task.FromResult(true);
        }
    }
}
