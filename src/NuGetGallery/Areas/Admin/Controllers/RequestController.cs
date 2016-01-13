using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NuGetGallery.Areas.Admin.Models;
using NuGetGallery.Areas.Admin.ViewModels;

namespace NuGetGallery.Areas.Admin.Controllers
{
    public class RequestController : AdminControllerBase
    {
        public ActionResult Create()
        {
            var admin = new AdminModel();
            var issueStatus = new IssueStatusModel();
            var createView = new CreateViewModel();

            var allIssueStatuses = issueStatus.GetAllIssueStatuses();
            var allAdmins = admin.GetAllAdmins();

            createView.AssignedToChoices = GetListOfAdmins(allAdmins, -1);
            createView.IssueStatusNameChoices = GetListOfIssueStatuses(allIssueStatuses, -1);
            createView.ReasonChoices = GetListOfReasons(string.Empty);

            return View(createView);
        }

        [HttpPost]
        public ActionResult Create(CreateViewModel createViewModel)
        {
            var issue = new IssueModel();
            var newIssue = createViewModel.Issue;

            if (ModelState.IsValid)
            {
                newIssue.CreatedDate = DateTime.UtcNow;
                newIssue.AssignedTo = newIssue.AssignedTo ?? 0;
                newIssue.IssueStatus = newIssue.IssueStatus ?? 1;
                newIssue.Reason = String.IsNullOrEmpty(newIssue.Reason) ? "Other" : newIssue.Reason;
                issue.AddIssue(newIssue, GetLoggedInUser());
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
            var issue = new IssueModel();
            var issueId = issueViewModel.Issue.Id;

            var newAssignedTo = issueViewModel.AssignedTo ?? -1;
            var newIssueStatusName = issueViewModel.IssueStatusName ?? -1;

            var currentIssue = issue.GetIssueById(issueId);

            if (issue != null)
            {
                if (newAssignedTo != -1)
                {
                    currentIssue.AssignedTo = newAssignedTo;
                }

                if (newIssueStatusName != -1)
                {
                    currentIssue.IssueStatus = newIssueStatusName;
                }
                issue.SaveChanges();
               
                issue.AddHistoryEntry(currentIssue, GetLoggedInUser());
            }

            return RedirectToAction("index", new
            {
                passedInAssignedToFilter = issueViewModel.CurrentAssignedToFilter,
                passedInIssueStatusNameFilter = issueViewModel.CurrentIssueStatusNameFilter,
                passedInReasonFilter = issueViewModel.CurrentReasonFilter,
                passedInStatusId = issueViewModel.CurrentStatusId,
                passedInPageNumber = issueViewModel.CurrentPageNumber
            });
        }

        [HttpPost]
        public ActionResult Filter([Bind(Prefix = "Filter")]FilterResultsViewModel filterResultsView)
        {
            return RedirectToAction("index", new
            {
                passedInAssignedToFilter = filterResultsView.AssignedTo,
                passedInIssueStatusNameFilter = filterResultsView.IssueStatusName,
                passedInReasonFilter = filterResultsView.Reason,
                passedInStatusId = filterResultsView.StatusID,
                passedInPageNumber = filterResultsView.PageNumber
            });
        }
       
        public ActionResult index(int passedInPageNumber = 0,
            int passedInStatusId = 1,
            int? passedInAssignedToFilter = -1,
            int? passedInIssueStatusNameFilter = -1,
            string passedInReasonFilter = "[Reason]")
        {
            IndexViewModel indexViewModel = new IndexViewModel();
            var issues = GetIssues(passedInStatusId);

            if (passedInAssignedToFilter.HasValue && 
                passedInAssignedToFilter != -1)
            {
                issues = issues.Where(r => r.AssignedTo == passedInAssignedToFilter).ToList();
            }

            if (passedInIssueStatusNameFilter.HasValue &&
                passedInIssueStatusNameFilter != -1)
            {
                issues = issues.Where(r => r.IssueStatus == passedInIssueStatusNameFilter).ToList();
            }

            if (!String.IsNullOrEmpty(passedInReasonFilter) && 
                !String.Equals(passedInReasonFilter, "[Reason]", StringComparison.OrdinalIgnoreCase))
            {
                issues = issues.Where(r => r.Reason == passedInReasonFilter).ToList();
            }

            if (issues.Count == 0)
            {
                ViewBag.Title = "No issues to display";
                return View();
            }

            const int PageSize = 5;

            var count = issues.Count();

            var data = issues.Skip(passedInPageNumber * PageSize).Take(PageSize).ToList();

            if (count % PageSize == 0)
            {
                ViewBag.MaxPage = count / PageSize;
            }
            else
            {
                ViewBag.MaxPage = count / PageSize + 1;
            }

            ViewBag.Page = passedInPageNumber;
            SetViewBagForIndexView();
            indexViewModel = CreateIndexViewModel(data, passedInAssignedToFilter,
                passedInIssueStatusNameFilter,
                passedInReasonFilter,
                passedInStatusId,
                passedInPageNumber);

            return View(indexViewModel);
        }

        [HttpPost]
        public ActionResult index(IndexViewModel indexViewModel)
        {
            return RedirectToAction("index", new
            {
                passedInAssignedToFilter = indexViewModel.Filter.AssignedTo,
                passedInIssueStatusNameFilter = indexViewModel.Filter.IssueStatusName,
                passedInReasonFilter = indexViewModel.Filter.Reason,
                passedInStatusId = indexViewModel.Filter.StatusID,
                passedInPageNumber = indexViewModel.Filter.PageNumber
            });
        }

        public ActionResult Edit(int id = 1,
            int passedInPageNumber = 0, int passedInStatusId = 1,
            int passedInAssignedToFilter = -1, int passedInIssueStatusNameFilter = -1,
            string passedInReasonFilter = "[Reason]")
        {
            var admin = new AdminModel();
            var issue = new IssueModel();
            var issueStatus = new IssueStatusModel();
            var editView = new EditViewModel();

            var currentRequest = issue.GetIssueById(id);

            if (currentRequest == null)
            {
                ViewBag.Title = "Issue with id " + id + " not found";
                return View();
            }

            editView.Issue = currentRequest;
            editView.AssignedToLabel = admin.GetUserNameById(currentRequest.AssignedTo ?? 0);
            editView.IssueStatusNameLabel = issueStatus.GetIssueStatusNameById(currentRequest.IssueStatus ?? 1);
            editView.CurrentAssignedToFilter = passedInAssignedToFilter;
            editView.CurrentIssueStatusNameFilter = passedInIssueStatusNameFilter;
            editView.CurrentPageNumber = passedInPageNumber;
            editView.CurrentReasonFilter = passedInReasonFilter;
            editView.CurrentStatusId = passedInStatusId;

            return View(editView);
        }

        [HttpPost]
        public ActionResult Edit(EditViewModel editViewModel)
        {
            var issue = new IssueModel();
            var currentIssue = editViewModel.Issue;

            if (ModelState.IsValid)
            {
                issue.Entry(currentIssue).State = System.Data.Entity.EntityState.Modified;
                issue.SaveChanges();

                issue.AddHistoryEntry(currentIssue, GetLoggedInUser());

                return RedirectToAction("index", new
                {
                    passedInAssignedToFilter = editViewModel.CurrentAssignedToFilter,
                    passedInIssueStatusNameFilter = editViewModel.CurrentIssueStatusNameFilter,
                    passedInReasonFilter = editViewModel.CurrentReasonFilter,
                    passedInStatusId = editViewModel.CurrentStatusId,
                    passedInPageNumber = editViewModel.CurrentPageNumber
                });
            }

            return RedirectToAction("Edit", new { id = currentIssue.Id, passedInAssignedToFilter = editViewModel.CurrentAssignedToFilter,
                                            passedInIssueStatusNameFilter = editViewModel.CurrentIssueStatusNameFilter,
                                            passedInReasonFilter = editViewModel.CurrentReasonFilter,
                                            passedInStatusId = editViewModel.CurrentStatusId,
                                            passedInPageNumber = editViewModel.CurrentPageNumber});
        }

        public ActionResult History(int id = 1,
            int passedInPageNumber = 0, int passedInStatusId = 1,
            int passedInAssignedToFilter = -1, int passedInIssueStatusNameFilter = -1,
            string passedInReasonFilter = "[Reason]")
        {
            var history = new HistoryModel();
            var admin = new AdminModel();
            var issue = new IssueModel();

            var issueHistories = new List<HistoryListModel>();
            var historyViewModel = new HistoryViewModel();

            var historyEntries = history.GetHistoryEntriesByIssueKey(id);

            if (historyEntries != null && historyEntries.Count > 0)
            {
                var currentIssue = issue.GetIssueById(id);
                if (currentIssue != null)
                {
                    var issueTitle = issue.GetIssueById(id).IssueTitle;
                    ViewBag.IssueTitle = issueTitle;
                }

                foreach (History h in historyEntries)
                {
                    var edited = admin.GetUserNameById(h.EditedBy);
                    var ih = new HistoryListModel();
                    ih.History = h;
                    ih.EditedBy = edited;
                    issueHistories.Add(ih);

                }

                historyViewModel.HistoryList = issueHistories;
                historyViewModel.CurrentAssignedToFilter = passedInAssignedToFilter;
                historyViewModel.CurrentIssueStatusNameFilter = passedInIssueStatusNameFilter;
                historyViewModel.CurrentPageNumber = passedInPageNumber;
                historyViewModel.CurrentReasonFilter = passedInReasonFilter;
                historyViewModel.CurrentStatusId = passedInStatusId;

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

        private string VerifyAndFixTralingSlash(string p)
        {
            var returnVal = p;
            if (!String.IsNullOrEmpty(p) && p.Substring(p.Length - 1, 1) != "/")
            {
                returnVal = String.Concat(p, "/");
            }
            return returnVal;
        }

        private List<SelectListItem> GetListOfIssueStatuses(List<IssueStatus> incoming, int? issueToSelect)
        {
            var items = new List<SelectListItem>();

            foreach (var i in incoming)
            {
                var s = new SelectListItem { Text = i.StatusName, Value = i.Id.ToString() };
                items.Add(s);
                if (issueToSelect.HasValue && i.Id == issueToSelect)
                {
                    s.Selected = true;
                }
            }

            return items;
        }

        private List<SelectListItem> GetListOfReasons(string reasonToSelect)
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

        private List<SelectListItem> GetListOfAdmins(List<NuGetGallery.Areas.Admin.Models.Admin> incoming, 
                                                        int? adminToSelect)
        {
            var admins = new List<SelectListItem>();

            foreach (var a in incoming)
            {
                var currentItem = new SelectListItem { Text = a.UserName, Value = a.Id.ToString() };
                if (adminToSelect.HasValue && a.Id == adminToSelect)
                {
                    currentItem.Selected = true;
                }
                admins.Add(currentItem);
            }

            return admins;
        }

        private void SetViewBagForIndexView()
        {
            var issue = new IssueModel();
            ViewBag.OpenCount = issue.GetCountOfOpenIssues();
            ViewBag.ResolvedCount = issue.GetCountOfResolvedIssues();
            ViewBag.UnAssignedCount = issue.GetCountOfUnassignedIssues();
        }

        private List<Issue> GetIssues(int statusID)
        {
            var issue = new IssueModel();
            var issues = new List<Issue>();
            if (statusID == 2)
            {
                issues = issue.GetResolvedIssues();
                ViewBag.Title = "Resolved Support Requests";
            }
            else if (statusID == 3)
            {
                issues = issue.GetAllIssues();
                ViewBag.Title = "All Support Requests";
            }
            else if (statusID == 4)
            {
                issues = issue.GetUnassignedIssues();
                ViewBag.Title = "Unassigned Support Requests";
            }
            else
            {
                issues = issue.GetOpenIssues();
                ViewBag.Title = "Open Support Requests";
            }
            return issues;
        }

        private IndexViewModel CreateIndexViewModel(List<Issue> filteredIssues,
            int? passedInAssignedToFilter, int? passedInIssueStatusNameFilter,
            string passedInReasonFilter, int passedInStatusId, int passedInPageNumber)
        {
            var admin = new AdminModel();
            var issueStatus = new IssueStatusModel();

            var indexViewModel = new IndexViewModel();
            var filterResultsViewModel = new FilterResultsViewModel();
            var filteredIssueViews = new List<IssueViewModel>();

            var admins = admin.GetAllAdmins();
            var issueStatuses = issueStatus.GetAllIssueStatuses();
            var adminsList = GetListOfAdmins(admins, -1);
            var issueStatusesList = GetListOfIssueStatuses(issueStatuses, -1);

            foreach (Issue i in filteredIssues)
            {
                var rv = new IssueViewModel();
                rv.Issue = i;
                rv.AssignedToLabel = admin.GetUserNameById(i.AssignedTo ?? 0);
                rv.IssueStatusNameLabel = issueStatus.GetIssueStatusNameById(i.IssueStatus ?? 1);
                rv.AssignedToChoices = adminsList;
                rv.IssueStatusNameChoices = issueStatusesList;
                rv.Issue.SiteRoot = VerifyAndFixTralingSlash(rv.Issue.SiteRoot);
                rv.CurrentPageNumber = passedInPageNumber;
                rv.CurrentStatusId = passedInStatusId;
                rv.CurrentAssignedToFilter = passedInAssignedToFilter ?? -1;
                rv.CurrentIssueStatusNameFilter = passedInIssueStatusNameFilter ?? -1;
                rv.CurrentReasonFilter = passedInReasonFilter;
                filteredIssueViews.Add(rv);
            }

            filterResultsViewModel.AssignedToChoices = GetListOfAdmins(admins, passedInAssignedToFilter);
            filterResultsViewModel.IssueStatusNameChoices = GetListOfIssueStatuses(issueStatuses, passedInIssueStatusNameFilter);
            filterResultsViewModel.PageNumber = passedInPageNumber;
            filterResultsViewModel.StatusID = passedInStatusId;
            filterResultsViewModel.ReasonChoices = GetListOfReasons(passedInReasonFilter);

            indexViewModel.Issues = filteredIssueViews;
            indexViewModel.Filter = filterResultsViewModel;

            return indexViewModel;
        }
        #endregion
    }
}