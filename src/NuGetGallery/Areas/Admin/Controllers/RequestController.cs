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
        IssueModel issue = new IssueModel();
        AdminModel admin = new AdminModel();
        HistoryModel history = new HistoryModel();

        IList<IssueViewModel> issueViews = new List<IssueViewModel>();
        IList<HistoryViewModel> issueHistories = new List<HistoryViewModel>();
        IssueStatusModel issueStatus = new IssueStatusModel();

        public ActionResult Create()
        {
            var issueStatues = issueStatus.GetAllIssueStatuses();
            var items = new List<SelectListItem>();

            foreach (var i in issueStatues)
            {
                SelectListItem s = new SelectListItem { Text = i.StatusName, Value = i.Id.ToString() };
                items.Add(s);

            }

            var allAdmins = admin.GetAllAdmins();
            var admins = new List<SelectListItem>();
            foreach (var a in allAdmins)
            {
                SelectListItem currentItem = new SelectListItem { Text = a.UserName, Value = a.Id.ToString() };

                admins.Add(currentItem);
            }

            var reasons = CreateListOfReasons(String.Empty);

            ViewBag.IssueStatus = items;
            ViewBag.AssignedTo = admins;
            ViewBag.Reason = reasons;

            return View();
        }

        [HttpPost]
        public ActionResult Create(Issue newissue)
        {

            if (ModelState.IsValid)
            {
                newissue.CreatedDate = DateTime.Now;
                issue.AddIssue(newissue);
                return RedirectToAction("Index");
            }
            else
            {
                return View(newissue);
            }
        }

        [HttpPost]
        public ActionResult Save(FormCollection formcollection)
        {
            //Values required for updating the issue
            var issueId = GetValue(formcollection["IssueId"]);
            var assignedTo = GetValue(formcollection["AssignedTo"]);
            var issueStatusName = GetValue(formcollection["IssueStatusName"]);

            var currentIssue = issue.GetIssueById(issueId);

            if (issue != null)
            {
                if (assignedTo != -1)
                {
                    currentIssue.AssignedTo = assignedTo;
                }

                if (issueStatusName != -1)
                {
                    currentIssue.IssueStatus = issueStatusName;
                }
                issue.SaveChanges();
                issue.AddHistoryEntry(currentIssue);
            }

            //Values required for remembering the filters applied
            var reasonFilter = String.Empty;

            if (formcollection["ReasonFilter"] != "/")
            {
                reasonFilter = formcollection["ReasonFilter"];
            }

            //Write values into Session variables
            Session["StatusId"] = GetValue(formcollection["StatusId"]);
            Session["AssignedToFilter"] = GetValue(formcollection["AssignedToFilter"]);
            Session["IssueStatusNameFilter"] = GetValue(formcollection["IssueStatusNameFilter"]);
            Session["ReasonFilter"] = reasonFilter;
            Session["PageNumber"] = GetValue(formcollection["Page"]);

            return RedirectToAction("Index");
        }

        [HttpPost]
        public ActionResult Index(FormCollection formCollection)
        {
            var currentUrl = this.Request.Url;
            var assignedToFilter = GetValue(formCollection["AssignedToFilter"]);
            var issueStatusNameFilter = GetValue(formCollection["issueStatusNameFilter"]);

            var reasonFilter = formCollection["Reason"];
            var pageNumber = GetValue(formCollection["Page"]);
            var statusID = GetValue(formCollection["StatusId"]);

            if (Session["StatusId"] != null)
            {
                statusID = (int)Session["StatusId"];
            }
            else if (!String.IsNullOrEmpty(currentUrl.Query))
            {
                var input = currentUrl.Query.Substring(1);
                var items = input.Split(new[] { '&' });
                var dict = items.Select(item => item.Split(new[] { '=' })).ToDictionary(pair => pair[0], pair => pair[1]);
                statusID = GetValue(dict["statusID"]);
            }

            if (statusID == -1)
            {
                statusID = 1;
            }

            //Write values into Session variables
            Session["StatusId"] = statusID;
            Session["AssignedToFilter"] = assignedToFilter;
            Session["IssueStatusNameFilter"] = issueStatusNameFilter;
            Session["ReasonFilter"] = reasonFilter;
            Session["PageNumber"] = pageNumber;

            return RedirectToAction("Index");
        }

        public ActionResult Index(int passedInPageNumber = -100, int passedInStatusId = -100, int passedInAssignedToFilter = -100, int passedInIssueStatusNameFilter = -100, string passedInReasonFilter = "")
        {
            //Read values from Session variables
            var statusID = (passedInStatusId == -100) ? ((Session["StatusId"] == null) ? 1 : (int)Session["StatusId"]) : passedInStatusId;
            var assignedToFilter = (passedInAssignedToFilter == -100) ? ((Session["AssignedToFilter"] == null) ? -1 : (int)Session["AssignedToFilter"]) : passedInAssignedToFilter;
            var issueStatusNameFilter = (passedInIssueStatusNameFilter == -100) ? ((Session["IssueStatusNameFilter"] == null) ? -1 : (int)Session["IssueStatusNameFilter"]) : passedInIssueStatusNameFilter;
            var reasonFilter = (passedInReasonFilter == "clear") ? String.Empty : ((Session["ReasonFilter"] == null) ? string.Empty : (string)Session["ReasonFilter"]);
            var page = (passedInPageNumber == -100) ? ((Session["PageNumber"] == null) ? 0 : (int)Session["PageNumber"]) : passedInPageNumber;

            var issues = GetIssues(statusID);

            if (assignedToFilter != -1)
            {
                issues = issues.Where(r => r.AssignedTo == assignedToFilter).ToList();
            }

            if (issueStatusNameFilter != -1)
            {
                issues = issues.Where(r => r.IssueStatus == issueStatusNameFilter).ToList();
            }

            if (!String.IsNullOrEmpty(reasonFilter))
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

            var data = issues.Skip(page * PageSize).Take(PageSize).ToList();

            if (count % PageSize == 0)
            {
                ViewBag.MaxPage = count / PageSize;
            }
            else
            {
                ViewBag.MaxPage = count / PageSize + 1;
            }

            ViewBag.Page = page;

            SetViewBagForIndexView(assignedToFilter, issueStatusNameFilter, reasonFilter);

            //Write values into Session variables
            Session["StatusId"] = statusID;
            Session["AssignedToFilter"] = assignedToFilter;
            Session["IssueStatusNameFilter"] = issueStatusNameFilter;
            Session["ReasonFilter"] = reasonFilter;
            Session["PageNumber"] = page;

            issueViews = CreateIssueViews(data);
            return View(issueViews);
        }
               
        public ActionResult Edit(int id = 1)
        {
            var currentRequest = issue.GetIssueById(id);

            if (currentRequest == null)
            {
                ViewBag.Title = "Issue with id " + id + " not found";
                return View();
            }

            ViewBag.AssignedTo = admin.GetUserNameById(currentRequest.AssignedTo);
            ViewBag.IssueStatus = issueStatus.GetIssueStatusNameById(currentRequest.IssueStatus);

            return View(currentRequest);
        }

        public ActionResult History(int id = 1)
        {
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
                    var ih = new HistoryViewModel();
                    ih.History = h;
                    ih.EditedBy = edited;
                    issueHistories.Add(ih);

                }
                ViewBag.Title = String.Empty;
                return View(issueHistories);
            }
            ViewBag.Title = "No history to display";
            return View();
        }

        [HttpPost]
        public ActionResult Edit(Issue currentRequest, FormCollection formCollection)
        {
            if (ModelState.IsValid)
            {
                issue.Entry(currentRequest).State = System.Data.Entity.EntityState.Modified;
                issue.SaveChanges();

                issue.AddHistoryEntry(currentRequest);

                //Get filtering values
                int statusID = GetValue(formCollection["StatusId"]);

                if (statusID == -1)
                {
                    statusID = 1;
                }

                //Write values into Session variables
                Session["StatusId"] = statusID;
                Session["AssignedToFilter"] = GetValue(formCollection["AssignedToFilter"]);
                Session["IssueStatusNameFilter"] = GetValue(formCollection["issueStatusNameFilter"]);
                Session["ReasonFilter"] = (formCollection["ReasonFilter"] == "/") ? String.Empty : formCollection["ReasonFilter"];
                Session["PageNumber"] = GetValue(formCollection["Page"]);
                return RedirectToAction("Index");
            }

            return RedirectToAction("Edit", new { id = currentRequest.Id });
        }

        #region private

        private List<IssueViewModel> CreateIssueViews(List<Issue> filteredIssues)
        {
            var filteredIssueViews = new List<IssueViewModel>();
            var admins = admin.GetAllAdmins();
            var issueStatuses = issueStatus.GetAllIssueStatuses();

            foreach (Issue i in filteredIssues)
            {
                var rv = new IssueViewModel();
                rv.Issue = i;
                rv.AssignedToLabel = admin.GetUserNameById(i.AssignedTo);
                rv.IssueStatusNameLabel = issueStatus.GetIssueStatusNameById(i.IssueStatus);
                rv.AssignedTo = GetListOfAdmins(admins, -1);
                rv.IssueStatusName = GetListOfIssueStatuses(issueStatuses, -1);
                rv.OwnerLink = string.Empty;
                rv.Issue.SiteRoot = VerifyAndFixTralingSlash(rv.Issue.SiteRoot);

                if (!rv.Issue.CreatedBy.Equals("Anonymous", StringComparison.OrdinalIgnoreCase))
                {
                    rv.OwnerLink = String.Concat(rv.Issue.SiteRoot, "Profiles", "/",  rv.Issue.CreatedBy);
                }
                filteredIssueViews.Add(rv);

                rv.PackageLink = String.Concat(rv.Issue.SiteRoot, "Packages", "/", rv.Issue.PackageID, "/", rv.Issue.PackageVersion);
            }
            return filteredIssueViews;
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

        private List<SelectListItem> CreateListOfReasons(string reasonToSelect)
        {
            var reasons = new List<SelectListItem>();
            var item = new SelectListItem { Text = "Other" };
            reasons.Add(item);
            item = new SelectListItem { Text = "HasABugOrFailedToInstall" };
            reasons.Add(item);
            item = new SelectListItem { Text = "ContainsMaliciousCode" };
            reasons.Add(item);
            item = new SelectListItem { Text = "ViolatesALicenseIOwn" };
            reasons.Add(item);
            item = new SelectListItem { Text = "IsFradulent" };
            reasons.Add(item);
            item = new SelectListItem { Text = "ContainsPrivateAndConfidentialData" };
            reasons.Add(item);
            item = new SelectListItem { Text = "PublishedWithWrongVersion" };
            reasons.Add(item);
            item = new SelectListItem { Text = "ReleasedInPublicByAccident}" };
            reasons.Add(item);
            foreach (var r in reasons)
            {
                if (String.Equals(reasonToSelect, r.Text, StringComparison.OrdinalIgnoreCase))
                {
                    r.Selected = true;
                }
            }
            return reasons;
        }
        private List<SelectListItem> GetListOfIssueStatuses(List<IssueStatus> incoming, int issueToSelect)
        {
            var items = new List<SelectListItem>();

            foreach (var i in incoming)
            {
                var s = new SelectListItem { Text = i.StatusName, Value = i.Id.ToString() };
                items.Add(s);
                if (i.Id == issueToSelect)
                {
                    s.Selected = true;
                }
            }

            return items;
        }

        private List<SelectListItem> GetListOfAdmins(List<NuGetGallery.Areas.Admin.Models.Admin> incoming, int adminToSelect)
        {
            var admins = new List<SelectListItem>();

            foreach (var a in incoming)
            {
                var currentItem = new SelectListItem { Text = a.UserName, Value = a.Id.ToString() };
                if (a.Id == adminToSelect)
                {
                    currentItem.Selected = true;
                }
                admins.Add(currentItem);
            }
            return admins;
        }

        private void SetViewBagForIndexView(int assignedToFilter, int issueStatusNameFilter, string reason)
        {
            ViewBag.OpenCount = issue.GetCountOfOpenIssues();
            ViewBag.ResolvedCount = issue.GetCountOfResolvedIssues();
            ViewBag.UnAssignedCount = issue.GetCountOfUnassignedIssues();
            ViewBag.AssignedTo = GetListOfAdmins(admin.GetAllAdmins(), assignedToFilter);
            ViewBag.AssignedToFilter = GetListOfAdmins(admin.GetAllAdmins(), assignedToFilter);
            ViewBag.IssueStatusName = GetListOfIssueStatuses(issueStatus.GetAllIssueStatuses(), issueStatusNameFilter);
            ViewBag.IssueStatusNameFilter = GetListOfIssueStatuses(issueStatus.GetAllIssueStatuses(), issueStatusNameFilter);
            ViewBag.Reason = CreateListOfReasons(reason);
        }

        private List<Issue> GetIssues(int statusID)
        {
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

        private int GetValue(string input)
        {
            var i = -1;
            try
            {
                if (!String.IsNullOrEmpty(input))
                {
                    i = Convert.ToInt32(input);
                }
            }
            catch
            {

            }
            return i;
        }
        #endregion
    }
}