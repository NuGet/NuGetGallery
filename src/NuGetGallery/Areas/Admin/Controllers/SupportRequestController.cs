// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using System.Globalization;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SupportRequestController : AdminControllerBase
    {
        public ISupportRequestService SupportRequestService { get; protected set; }
        private readonly ISupportRequestDbContext _context;

        private const int DefaultAdminToSelect = -1;
        private const int DefaultIssueStatusToSelect = -1;

        private const string DefaultReasonToCreate = "Other";

        public SupportRequestController(ISupportRequestDbContext context)
        {
            _context = context;
            SupportRequestService = new SupportRequestService(_context);
        }

        public SupportRequestController(ISupportRequestService supportRequestService,
            ISupportRequestDbContext context)
        {
            SupportRequestService = supportRequestService;
            _context = context;
        }

        public ViewResult Create()
        {
            var createView = new CreateViewModel();

            var allIssueStatuses = SupportRequestService.GetAllIssueStatuses();
            var allAdmins = SupportRequestService.GetAllAdmins();

            createView.AssignedToChoices = GetListOfAdmins(allAdmins, DefaultAdminToSelect);
            createView.IssueStatusNameChoices = GetListOfIssueStatuses(allIssueStatuses, DefaultIssueStatusToSelect);
            createView.ReasonChoices = GetListOfReasons(string.Empty);

            return View(createView);
        }

        [HttpPost]
        public ActionResult Create(CreateViewModel createViewModel)
        {
            var newIssue = createViewModel.Issue;

            if (ModelState.IsValid)
            {
                newIssue.CreatedDate = DateTime.UtcNow;
                newIssue.AssignedTo = newIssue.AssignedTo ?? 0;
                newIssue.IssueStatus = newIssue.IssueStatus ?? 1;
                newIssue.Reason = String.IsNullOrEmpty(newIssue.Reason) ? DefaultReasonToCreate : newIssue.Reason;
                SupportRequestService.AddIssue(newIssue, GetLoggedInUser());
                return RedirectToAction("index");
            }
            else
            {
                return View(newIssue);
            }
        }
  
        [HttpPost]
        public ActionResult Save([Bind(Prefix = "item")]IssueViewModel issueViewModel)
        {
            //Values required for updating the issue
            var issueId = issueViewModel.Issue.Key;

            var newAssignedTo = issueViewModel.AssignedTo ?? -1;
            var newIssueStatusName = issueViewModel.IssueStatusName ?? -1;

            var currentIssue = SupportRequestService.GetIssueById(issueId);

            if (currentIssue != null)
            {
                if (newAssignedTo != -1)
                {
                    currentIssue.AssignedTo = newAssignedTo;
                }

                if (newIssueStatusName != -1)
                {
                    currentIssue.IssueStatus = newIssueStatusName;
                }

                _context.CommitChanges();
                SupportRequestService.AddHistoryEntry(currentIssue, GetLoggedInUser());
            }

            return RedirectToAction("index", new
            {
                assignedToFilter = issueViewModel.CurrentAssignedToFilter,
                issueStatusNameFilter = issueViewModel.CurrentIssueStatusNameFilter,
                reasonFilter = issueViewModel.CurrentReasonFilter,
                statusId = issueViewModel.CurrentStatusId,
                pageNumber = issueViewModel.CurrentPageNumber
            });
        }

        [HttpPost]
        public ActionResult Filter([Bind(Prefix = "Filter")]FilterResultsViewModel filterResultsView)
        {
            return RedirectToAction("index", new
            {
                assignedToFilter = filterResultsView.AssignedTo,
                issueStatusNameFilter = filterResultsView.IssueStatusName,
                reasonFilter = filterResultsView.Reason,
                statusId = filterResultsView.StatusID,
                pageNumber = filterResultsView.PageNumber
            });
        }
       
        public ViewResult index(int pageNumber = 0,
            int statusId = 1,
            int? assignedToFilter = -1,
            int? issueStatusNameFilter = -1,
            string reasonFilter = "[Reason]")
        {
            IndexViewModel indexViewModel = new IndexViewModel();
            var issues = GetIssues(statusId);

            if (assignedToFilter.HasValue && 
                assignedToFilter != -1)
            {
                issues = issues.Where(r => r.AssignedTo == assignedToFilter).ToList();
            }

            if (issueStatusNameFilter.HasValue &&
                issueStatusNameFilter != -1)
            {
                issues = issues.Where(r => r.IssueStatus == issueStatusNameFilter).ToList();
            }

            if (!String.IsNullOrEmpty(reasonFilter) && 
                !String.Equals(reasonFilter, "[Reason]", StringComparison.OrdinalIgnoreCase))
            {
                issues = issues.Where(r => r.Reason == reasonFilter).ToList();
            }

            if (issues.Count == 0)
            {
                ViewBag.Title = "No issues to display";
                return View();
            }

            const int PageSize = 5;

            var count = issues.Count();

            var data = issues.Skip(pageNumber * PageSize).Take(PageSize).ToList();

            if (count % PageSize == 0)
            {
                ViewBag.MaxPage = count / PageSize;
            }
            else
            {
                ViewBag.MaxPage = count / PageSize + 1;
            }

            ViewBag.Page = pageNumber;
            SetViewBagForIndexView();
            indexViewModel = CreateIndexViewModel(data, assignedToFilter,
                issueStatusNameFilter,
                reasonFilter,
                statusId,
                pageNumber);

            return View(indexViewModel);
        }

        [HttpPost]
        public ActionResult index(IndexViewModel indexViewModel)
        {
            return RedirectToAction("index", new
            {
                assignedToFilter = indexViewModel.Filter.AssignedTo,
                issueStatusNameFilter = indexViewModel.Filter.IssueStatusName,
                reasonFilter = indexViewModel.Filter.Reason,
                statusId = indexViewModel.Filter.StatusID,
                pageNumber = indexViewModel.Filter.PageNumber
            });
        }

        public ActionResult Edit(int id = 1,
            int pageNumber = 0, int statusId = 1,
            int assignedToFilter = -1, int issueStatusNameFilter = -1,
            string reasonFilter = "[Reason]")
        {
            var editView = new EditViewModel();

            var currentRequest = SupportRequestService.GetIssueById(id);

            if (currentRequest == null)
            {
                ViewBag.Title = "Issue with id " + id + " not found";
                return View();
            }

            editView.Issue = currentRequest;
            editView.AssignedToLabel = SupportRequestService.GetUserNameById(currentRequest.AssignedTo ?? 0);
            editView.IssueStatusNameLabel = SupportRequestService.GetIssueStatusNameById(currentRequest.IssueStatus ?? 1);
            editView.CurrentAssignedToFilter = assignedToFilter;
            editView.CurrentIssueStatusNameFilter = issueStatusNameFilter;
            editView.CurrentPageNumber = pageNumber;
            editView.CurrentReasonFilter = reasonFilter;
            editView.CurrentStatusId = statusId;

            return View(editView);
        }

        [HttpPost]
        public ActionResult Edit(EditViewModel editViewModel)
        {
            var currentIssue = editViewModel.Issue;

            if (ModelState.IsValid)
            {
                _context.CommitChanges();

                SupportRequestService.AddHistoryEntry(currentIssue, GetLoggedInUser());

                return RedirectToAction("index", new
                {
                    assignedToFilter = editViewModel.CurrentAssignedToFilter,
                    issueStatusNameFilter = editViewModel.CurrentIssueStatusNameFilter,
                    reasonFilter = editViewModel.CurrentReasonFilter,
                    statusId = editViewModel.CurrentStatusId,
                    pageNumber = editViewModel.CurrentPageNumber
                });
            }

            return RedirectToAction("Edit", new { id = currentIssue.Key, assignedToFilter = editViewModel.CurrentAssignedToFilter,
                                            issueStatusNameFilter = editViewModel.CurrentIssueStatusNameFilter,
                                            reasonFilter = editViewModel.CurrentReasonFilter,
                                            statusId = editViewModel.CurrentStatusId,
                                            pageNumber = editViewModel.CurrentPageNumber});
        }

        public ActionResult History(int id = 1,
            int pageNumber = 0, int statusId = 1,
            int assignedToFilter = -1, int issueStatusNameFilter = -1,
            string reasonFilter = "[Reason]")
        {
  
            var issueHistories = new List<HistoryListModel>();
            var historyViewModel = new HistoryViewModel();

            var historyEntries = SupportRequestService.GetHistoryEntriesByIssueKey(id);

            if (historyEntries != null && historyEntries.Count > 0)
            {
                var currentIssue = SupportRequestService.GetIssueById(id);
                if (currentIssue != null)
                {
                    var issueTitle = SupportRequestService.GetIssueById(id).IssueTitle;
                    ViewBag.IssueTitle = issueTitle;
                }

                foreach (History h in historyEntries)
                {
                    var edited = SupportRequestService.GetUserNameById(h.EditedBy);
                    var ih = new HistoryListModel();
                    ih.History = h;
                    ih.EditedBy = edited;
                    issueHistories.Add(ih);

                }

                historyViewModel.HistoryList = issueHistories;
                historyViewModel.CurrentAssignedToFilter = assignedToFilter;
                historyViewModel.CurrentIssueStatusNameFilter = issueStatusNameFilter;
                historyViewModel.CurrentPageNumber = pageNumber;
                historyViewModel.CurrentReasonFilter = reasonFilter;
                historyViewModel.CurrentStatusId = statusId;

                ViewBag.Title = String.Empty;
                return View(historyViewModel);
            }
            ViewBag.Title = "No history to display";
            return View();
        }
           

        #region private

        private string GetLoggedInUser()
        {
            string loggedInUser = string.Empty;
            if (this.User != null)
            {
                loggedInUser = this.User.Identity.Name;
            }

            return loggedInUser;
        }

        private static string VerifyAndFixTralingSlash(string url)
        {
            var returnVal = url;
            if (!String.IsNullOrEmpty(url) && url.Substring(url.Length - 1, 1) != "/")
            {
                returnVal = String.Concat(url, "/");
            }
            return returnVal;
        }

        private static List<SelectListItem> GetListOfIssueStatuses(List<IssueStatus> incoming, int? issueToSelect)
        {
            var items = new List<SelectListItem>();

            foreach (var i in incoming)
            {
                var s = new SelectListItem { Text = i.StatusName, Value = i.Key.ToString(CultureInfo.InvariantCulture) };
                items.Add(s);
                if (issueToSelect.HasValue && i.Key == issueToSelect)
                {
                    s.Selected = true;
                }
            }

            return items;
        }

        private static List<SelectListItem> GetListOfReasons(string reasonToSelect)
        {
            var reasons = new List<SelectListItem>();
            var reasonValues = System.Enum.GetValues(typeof(ReportPackageReason));

            int i = 0;
            foreach (var reasonValue in reasonValues)
            {
                var item = new SelectListItem { Text = reasonValue.ToString() };

                if (!String.IsNullOrEmpty(reasonToSelect) && 
                    String.Equals(reasonValue.ToString(), reasonToSelect, StringComparison.OrdinalIgnoreCase))
                {
                    item.Selected = true;
                }

                i++;
                reasons.Add(item);
            }

            return reasons;
        }

        private static List<SelectListItem> GetListOfAdmins(List<NuGetGallery.Areas.Admin.Models.Admin> incoming, 
                                                        int? adminToSelect)
        {
            var admins = new List<SelectListItem>();

            foreach (var a in incoming)
            {
                var currentItem = new SelectListItem { Text = a.UserName, Value = a.Key.ToString(CultureInfo.InvariantCulture) };
                if (adminToSelect.HasValue && a.Key == adminToSelect)
                {
                    currentItem.Selected = true;
                }
                admins.Add(currentItem);
            }

            return admins;
        }

        private void SetViewBagForIndexView()
        {
            ViewBag.OpenCount = SupportRequestService.GetCountOfOpenIssues();
            ViewBag.ResolvedCount = SupportRequestService.GetCountOfResolvedIssues();
            ViewBag.UnAssignedCount = SupportRequestService.GetCountOfUnassignedIssues();
        }

        private List<Issue> GetIssues(int statusID)
        {
            var issues = new List<Issue>();
            if (statusID == 2)
            {
                issues = SupportRequestService.GetResolvedIssues();
                ViewBag.Title = "Resolved Support Requests";
            }
            else if (statusID == 3)
            {
                issues = SupportRequestService.GetAllIssues();
                ViewBag.Title = "All Support Requests";
            }
            else if (statusID == 4)
            {
                issues = SupportRequestService.GetUnassignedIssues();
                ViewBag.Title = "Unassigned Support Requests";
            }
            else
            {
                issues = SupportRequestService.GetOpenIssues();
                ViewBag.Title = "Open Support Requests";
            }
            return issues;
        }

        private IndexViewModel CreateIndexViewModel(List<Issue> filteredIssues,
            int? assignedToFilter, int? issueStatusNameFilter,
            string reasonFilter, int statusId, int pageNumber)
        {
            var indexViewModel = new IndexViewModel();
            var filterResultsViewModel = new FilterResultsViewModel();
            var filteredIssueViews = new List<IssueViewModel>();

            var admins = SupportRequestService.GetAllAdmins();
            var issueStatuses = SupportRequestService.GetAllIssueStatuses();
            var adminsList = GetListOfAdmins(admins, DefaultAdminToSelect);
            var issueStatusesList = GetListOfIssueStatuses(issueStatuses, DefaultIssueStatusToSelect);

            foreach (Issue i in filteredIssues)
            {
                var rv = new IssueViewModel();
                rv.Issue = i;
                rv.AssignedToLabel = SupportRequestService.GetUserNameById(i.AssignedTo ?? 0);
                rv.IssueStatusNameLabel = SupportRequestService.GetIssueStatusNameById(i.IssueStatus ?? 1);
                rv.AssignedToChoices = adminsList;
                rv.IssueStatusNameChoices = issueStatusesList;
                rv.Issue.SiteRoot = VerifyAndFixTralingSlash(rv.Issue.SiteRoot);
                rv.CurrentPageNumber = pageNumber;
                rv.CurrentStatusId = statusId;
                rv.CurrentAssignedToFilter = assignedToFilter ?? -1;
                rv.CurrentIssueStatusNameFilter = issueStatusNameFilter ?? -1;
                rv.CurrentReasonFilter = reasonFilter;
                filteredIssueViews.Add(rv);
            }

            filterResultsViewModel.AssignedToChoices = GetListOfAdmins(admins, assignedToFilter);
            filterResultsViewModel.IssueStatusNameChoices = GetListOfIssueStatuses(issueStatuses, issueStatusNameFilter);
            filterResultsViewModel.PageNumber = pageNumber;
            filterResultsViewModel.StatusID = statusId;
            filterResultsViewModel.ReasonChoices = GetListOfReasons(reasonFilter);

            indexViewModel.Issues = filteredIssueViews;
            indexViewModel.Filter = filterResultsViewModel;

            return indexViewModel;
        }
        #endregion
    }
}