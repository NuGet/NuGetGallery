// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery;
using NuGetGallery.Configuration;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;
using System.Globalization;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class SupportRequestController : AdminControllerBase
    {
        public ISupportRequestService SupportRequestService { get; protected set; }
        private readonly ISupportRequestDbContext _context;
        private readonly IAppConfiguration _config;

        private const int DefaultAdminToSelect = -1;
        private const int DefaultIssueStatusToSelect = -1;

        private const string DefaultReasonToCreate = "Other";
        private const string UnAssignedAdminAccount = "unassigned";
        private const string NewIssueStatusName = "New";
       
        public SupportRequestController(ISupportRequestDbContext context)
            : this(context, null)
        {
        }

        public SupportRequestController(ISupportRequestDbContext context, IAppConfiguration config)
            :this(context, config, new SupportRequestService(context))
        {
           
        }

        public SupportRequestController(ISupportRequestDbContext context, 
            IAppConfiguration config, 
            ISupportRequestService supportRequestService)
        {
            SupportRequestService = supportRequestService;
            _context = context;
            _config = config;
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
                newIssue.CreatedBy = GetLoggedInUser();
                newIssue.CreatedDate = DateTime.UtcNow;
                newIssue.AssignedTo = newIssue.AssignedTo ?? SupportRequestService.GetAdminKeyFromUserName(UnAssignedAdminAccount);
                newIssue.IssueStatus = newIssue.IssueStatus ?? SupportRequestService.GetIssueStatusIdByName(NewIssueStatusName);
                newIssue.Comments = String.IsNullOrEmpty(newIssue.Comments) ? string.Empty : newIssue.Comments;
                newIssue.Reason = String.IsNullOrEmpty(newIssue.Reason) ? DefaultReasonToCreate : newIssue.Reason;
                newIssue.SiteRoot = String.IsNullOrEmpty(_config.SiteRoot) ? string.Empty : _config.SiteRoot;
                SupportRequestService.AddIssue(newIssue, GetLoggedInUser());
                return RedirectToAction("index");
            }
            else
            {
                return View(createViewModel);
            }
        }

        public ViewResult AddAdmin()
        {
            return View();
        }

        [HttpPost]
        public ActionResult AddAdmin(Models.Admin admin)
        {

            if (ModelState.IsValid)
            {
                SupportRequestService.AddAdmin(admin);
                return RedirectToAction("index");
            }
            else
            {
                return View(admin);
            }
        }

        public ViewResult DeleteAdmin()
        {
            ViewBag.DeleteMessage = string.Empty;
            return View();
        }

        [HttpPost]
        public ActionResult DeleteAdmin(Models.Admin admin)
        {

            if (ModelState.IsValid)
            {
                var retVal = SupportRequestService.DeleteAdmin(admin.UserName);
                if (retVal)
                {
                    return RedirectToAction("index");
                }
            }
            ViewBag.DeleteMessage = "Delete was not successful. Check the user name!";
            return View(admin);
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
                statusId = 6,
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
            editView.AssignedToLabel = SupportRequestService.GetGalleryUserNameById(currentRequest.AssignedTo ?? 
                                        SupportRequestService.GetAdminKeyFromUserName(UnAssignedAdminAccount));
            editView.IssueStatusNameLabel = SupportRequestService.GetIssueStatusNameById(currentRequest.IssueStatus ?? 
                                                SupportRequestService.GetIssueStatusIdByName(NewIssueStatusName));
            editView.CurrentAssignedToFilter = assignedToFilter;
            editView.CurrentIssueStatusNameFilter = issueStatusNameFilter;
            editView.CurrentPageNumber = pageNumber;
            editView.CurrentReasonFilter = reasonFilter;
            editView.CurrentStatusId = statusId;

            var admins = SupportRequestService.GetAllAdmins();
            var issueStatuses = SupportRequestService.GetAllIssueStatuses();
            var adminsList = GetListOfAdmins(admins, DefaultAdminToSelect);
            var issueStatusesList = GetListOfIssueStatuses(issueStatuses, DefaultIssueStatusToSelect);
            editView.AssignedToChoices = adminsList;
            editView.IssueStatusNameChoices = issueStatusesList;

            return View(editView);
        }

        [HttpPost]
        public ActionResult Edit(EditViewModel editViewModel)
        {
            var currentIssue = SupportRequestService.GetIssueById(editViewModel.Issue.Key);

            var newAssignedTo = editViewModel.AssignedTo ?? -1;
            var newIssueStatusName = editViewModel.IssueStatusName ?? -1;

            if (currentIssue != null && ModelState.IsValid)
            {
                if (newAssignedTo != -1)
                {
                    currentIssue.AssignedTo = newAssignedTo;
                }

                if (newIssueStatusName != -1)
                {
                    currentIssue.IssueStatus = newIssueStatusName;
                }

                currentIssue.Comments = editViewModel.Issue.Comments;

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
                    var edited = SupportRequestService.GetGalleryUserNameById(h.EditedBy);
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
                var currentItem = new SelectListItem { Text = a.GalleryUserName, Value = a.Key.ToString(CultureInfo.InvariantCulture) };
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
            ViewBag.MyAssignedCount = SupportRequestService.GetCountOfMyIssues(GetLoggedInUser());
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
            else if (statusID == 5)
            {
                issues = SupportRequestService.GetIssuesAssignedToMe(GetLoggedInUser());
                ViewBag.Title = "My Support Requests";
            }
            else if (statusID == 6)
            {
                issues = SupportRequestService.GetAllIssues();
                ViewBag.Title = "Filtered Support Requests";
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
                rv.AssignedToLabel = SupportRequestService.GetGalleryUserNameById(i.AssignedTo ?? 
                                        SupportRequestService.GetAdminKeyFromUserName(UnAssignedAdminAccount));
                rv.IssueStatusNameLabel = SupportRequestService.GetIssueStatusNameById(i.IssueStatus ?? 
                                            SupportRequestService.GetIssueStatusIdByName(NewIssueStatusName));
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
            filterResultsViewModel.ReasonChoices = GetListOfReasons(reasonFilter);

            filterResultsViewModel.PageNumber = pageNumber;
            filterResultsViewModel.StatusID = statusId;
            filterResultsViewModel.AssignedTo = assignedToFilter ?? -1;
            filterResultsViewModel.IssueStatusName = issueStatusNameFilter ?? -1;
            filterResultsViewModel.Reason = reasonFilter;

            indexViewModel.Issues = filteredIssueViews;
            indexViewModel.Filter = filterResultsViewModel;

            return indexViewModel;
        }
        #endregion
    }
}