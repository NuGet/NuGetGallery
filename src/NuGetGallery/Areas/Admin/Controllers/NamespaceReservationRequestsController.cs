// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using NuGet.Services.Entities;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class NamespaceReservationRequestsController : AdminControllerBase
    {
        internal const int PageSize = 50;
        private readonly IEntityRepository<NamespaceReservationRequest> _requests;

        public NamespaceReservationRequestsController(IEntityRepository<NamespaceReservationRequest> requests)
        {
            _requests = requests ?? throw new ArgumentNullException(nameof(requests));
        }

        [HttpGet]
        public ActionResult Index(string fromDate = null, string toDate = null, int page = 1)
        {
            var today = DateTime.UtcNow.Date;
            var model = new NamespaceReservationRequestsViewModel
            {
                FromDate = fromDate ?? today.AddDays(-6).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ToDate = toDate ?? today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Page = page
            };

            var validFrom = TryReadDate(model.FromDate, nameof(fromDate), out var from);
            var validTo = TryReadDate(model.ToDate, nameof(toDate), out var to);
            if (validFrom && validTo && from > to)
            {
                ModelState.AddModelError(nameof(toDate), "The end date must be on or after the start date.");
            }

            if (page < 1 || page > int.MaxValue / PageSize)
            {
                ModelState.AddModelError(nameof(page), "Select a valid results page.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var query = _requests.GetAll().AsNoTracking().Where(r => r.CreatedTimestamp >= from);
            // Inclusive calendar dates, using an exclusive upper bound so fractional seconds
            // throughout the end date are included. Avoid overflowing the largest possible date.
            if (to < DateTime.MaxValue.Date)
            {
                var until = to.AddDays(1);
                query = query.Where(r => r.CreatedTimestamp < until);
            }

            var rows = query.OrderBy(r => r.CreatedTimestamp).ThenBy(r => r.Key)
                .Skip((page - 1) * PageSize)
                .Take(PageSize + 1)
                .Select(r => new NamespaceReservationRequestRowViewModel
                {
                    Key = r.Key,
                    Namespace = r.Namespace,
                    SubmittedBy = r.SubmittedByUser.Username,
                    RequestedOwnersJson = r.RequestedOwnersJson,
                    Justification = r.Justification,
                    CreatedTimestamp = r.CreatedTimestamp,
                    Status = r.Status,
                    Reason = r.Reason,
                    CompletedTimestamp = r.CompletedTimestamp
                })
                .ToList();

            model.HasNextPage = rows.Count > PageSize;
            model.Requests = rows.Take(PageSize).ToList();
            model.HasResults = true;
            return View(model);
        }

        private bool TryReadDate(string value, string field, out DateTime date)
        {
            if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                ModelState.AddModelError(field, "Enter a valid date in YYYY-MM-DD format.");
                return false;
            }

            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return true;
        }
    }
}