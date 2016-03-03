﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using NuGetGallery.Helpers;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SupportRequestController
        : AdminControllerBase
    {
        private const int _defaultTakeCount = 30;
        private readonly ISupportRequestService _supportRequestService;
        private readonly JsonSerializerSettings _defaultJsonSerializerSettings = new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.None
        };

        public SupportRequestController(ISupportRequestService supportRequestService)
        {
            _supportRequestService = supportRequestService;
        }

        public ViewResult Admins()
        {
            var viewModel = new SupportRequestAdminsViewModel();
            viewModel.Admins.AddRange(_supportRequestService.GetAllAdmins());
            return View(viewModel);
        }

        public ActionResult GetAdmins()
        {
            var admins = _supportRequestService.GetAllAdmins();

            var data = JsonConvert.SerializeObject(admins, _defaultJsonSerializerSettings);
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public async Task<ActionResult> DisableAdmin(int key)
        {
            try
            {
                await _supportRequestService.ToggleAdminAccessAsync(key, enabled: false);

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        [HttpPost]
        public async Task<ActionResult> EnableAdmin(int key)
        {
            try
            {
                await _supportRequestService.ToggleAdminAccessAsync(key, enabled: true);

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        [HttpPost]
        public async Task<ActionResult> AddAdmin(string galleryUsername, string pagerDutyUsername)
        {
            try
            {
                await _supportRequestService.AddAdminAsync(galleryUsername, pagerDutyUsername);

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        [HttpPost]
        public async Task<ActionResult> UpdateAdmin(int key, string galleryUsername, string pagerDutyUsername)
        {
            try
            {
                await _supportRequestService.UpdateAdminAsync(key, galleryUsername, pagerDutyUsername);

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        [HttpPost]
        public async Task<ActionResult> Save(int issueKey, int? assignedToId, int issueStatusId, string comment)
        {
            try
            {
                await _supportRequestService.UpdateIssueAsync(issueKey, assignedToId, issueStatusId, comment, GetLoggedInUser());

                return new HttpStatusCodeResult(HttpStatusCode.NoContent);
            }
            catch (ArgumentException)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        }

        public ActionResult Filter(int pageNumber = 1, int take = _defaultTakeCount, int? assignedToId = null, int? issueStatusId = null, string reason = null)
        {
            if (pageNumber <= 0)
            {
                pageNumber = 1;
            }

            if (take < 1)
            {
                take = _defaultTakeCount;
            }

            var issues = GetSupportRequests(pageNumber, take, assignedToId, reason, issueStatusId).Take(take).ToList();
            var totalCount = _supportRequestService.GetIssueCount(assignedToId, reason, issueStatusId);

            int maxPage;
            if (totalCount % take == 0)
            {
                maxPage = totalCount / take;
            }
            else
            {
                maxPage = totalCount / take + 1;
            }

            var result = new
            {
                Issues = issues,
                CurrentPageNumber = Math.Max(pageNumber, 1),
                MaxPage = Math.Max(maxPage, 1)
            };

            var data = JsonConvert.SerializeObject(result, _defaultJsonSerializerSettings);
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        public ActionResult Index(int pageNumber = 1, int take = _defaultTakeCount, int? assignedToId = null, int? issueStatusId = null, string reason = null)
        {
            if (pageNumber <= 0)
            {
                pageNumber = 1;
            }

            if (take < 1)
            {
                take = _defaultTakeCount;
            }

            var viewModel = new SupportRequestsViewModel();

            viewModel.ItemsPerPage = take;
            viewModel.CurrentPageNumber = pageNumber;
            viewModel.AssignedToChoices = GetListOfAdmins();
            viewModel.IssueStatusNameChoices = GetListOfIssueStatuses();
            viewModel.AssignedToFilter = assignedToId;
            viewModel.ReasonChoices = GetListOfReasons(reason);
            viewModel.IssueStatusIdFilter = issueStatusId;
            viewModel.ReasonFilter = reason;

            var totalCount = _supportRequestService.GetIssueCount(assignedToId, reason, issueStatusId);
            if (totalCount % take == 0)
            {
                viewModel.MaxPage = totalCount / take;
            }
            else
            {
                viewModel.MaxPage = totalCount / take + 1;
            }

            var issues = GetSupportRequests(pageNumber, take, assignedToId, reason, issueStatusId);

            viewModel.Issues.AddRange(issues);

            return View(viewModel);
        }

        public ActionResult History(int id)
        {
            var historyEntries = _supportRequestService.GetHistoryEntriesByIssueKey(id).OrderByDescending(h => h.EntryDate);

            return Json(historyEntries, JsonRequestBehavior.AllowGet);
        }

        private List<SupportRequestViewModel> GetSupportRequests(int pageNumber = 1, int take = _defaultTakeCount, int? assignedTo = null, string reason = null, int? issueStatusId = null)
        {
            if (pageNumber <= 0)
            {
                pageNumber = 1;
            }

            if (take < 1)
            {
                take = _defaultTakeCount;
            }

            var skip = (pageNumber - 1) * take;
            var galleryUsername = GetLoggedInUser();
            var issues = _supportRequestService.GetIssues(assignedTo, reason, issueStatusId, galleryUsername);
            IEnumerable<Issue> pagedIssues = issues;

            if (skip > 0)
            {
                pagedIssues = issues.Skip(skip);
            }

            pagedIssues = pagedIssues.Take(take);

            var results = new List<SupportRequestViewModel>();

            foreach (var issue in pagedIssues)
            {
                var viewModel = new SupportRequestViewModel();
                viewModel.AssignedTo = issue.AssignedToId;
                viewModel.AssignedToGalleryUsername = issue.AssignedTo?.GalleryUsername;
                viewModel.IssueStatusId = issue.IssueStatusId;
                viewModel.IssueStatusName = issue.IssueStatus.Name;
                viewModel.Issue = issue;
                viewModel.Issue.SiteRoot = VerifyAndFixTrailingSlash(viewModel.Issue.SiteRoot);

                results.Add(viewModel);
            }

            return results;
        }

        private string GetLoggedInUser()
        {
            return User.Identity.Name;
        }

        private static string VerifyAndFixTrailingSlash(string url)
        {
            var result = url;
            if (!string.IsNullOrEmpty(url) && url.Substring(url.Length - 1, 1) != "/")
            {
                result = string.Concat(url, "/");
            }
            return result;
        }

        private List<SelectListItem> GetListOfIssueStatuses(int? selectedIssueKey = -1)
        {
            var issueStatuses = _supportRequestService.GetAllIssueStatuses();

            var items = new List<SelectListItem>();
            foreach (var status in issueStatuses)
            {
                var item = new SelectListItem { Text = status.Name, Value = status.Key.ToString(CultureInfo.InvariantCulture) };
                if (selectedIssueKey.HasValue && status.Key == selectedIssueKey)
                {
                    item.Selected = true;
                }

                items.Add(item);
            }

            items.Add(new SelectListItem { Text = "Unresolved", Value = IssueStatusKeys.Unresolved.ToString(CultureInfo.InvariantCulture) });

            return items;
        }

        private static List<SelectListItem> GetListOfReasons(string reasonToSelect)
        {
            var reasons = new List<SelectListItem>();
            var reasonValues = Enum.GetValues(typeof(ReportPackageReason));

            foreach (var reasonValue in reasonValues)
            {
                var item = new SelectListItem { Text = EnumHelper.GetDescription((ReportPackageReason)reasonValue) };

                if (!string.IsNullOrEmpty(reasonToSelect)
                    && string.Equals(reasonValue.ToString(), reasonToSelect, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                }

                reasons.Add(item);
            }

            return reasons;
        }

        private List<SelectListItem> GetListOfAdmins(int? selectedAdminKey = null)
        {
            var results = new List<SelectListItem>
            {
                // Add the "unassigned" admin
                new SelectListItem {Text = "unassigned", Value = "-1", Selected = selectedAdminKey == -1}
            };

            var admins = _supportRequestService.GetAllAdmins();
            foreach (var a in admins.OrderBy(a => a.GalleryUsername))
            {
                var currentItem = new SelectListItem { Text = a.GalleryUsername, Value = a.Key.ToString(CultureInfo.InvariantCulture) };
                if (a.Key == selectedAdminKey)
                {
                    currentItem.Selected = true;
                }

                results.Add(currentItem);
            }

            return results;
        }
    }
}