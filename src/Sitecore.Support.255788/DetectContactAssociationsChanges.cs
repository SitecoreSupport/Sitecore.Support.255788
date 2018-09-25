namespace Sitecore.ListManagement.ContentSearch.Pipelines.UpdateContactAssociations
{
  using System;
  using System.Linq;
  using System.Linq.Expressions;
  using Sitecore.ContentSearch;
  using Sitecore.ContentSearch.Linq.Utilities;
  using Sitecore.ContentSearch.Security;
  using Sitecore.Diagnostics;
  using Sitecore.ListManagement.Configuration;
  using Sitecore.ListManagement.ContentSearch.Model;

  public class DetectContactAssociationsChanges : ListProcessor
  {
    public virtual void Process([NotNull] UpdateContactAssociationsArgs args)
    {
      Assert.ArgumentNotNull(args, "args");

      var contactList = args.ContactList;
      var source = contactList.Source;

      using (
        var searchContext =
          ContentSearchManager.GetIndex(ListManagementSettings.ContactsIndexName)
            .CreateSearchContext(SearchSecurityOptions.EnableSecurityCheck))
      {
        var initialContacts = this.ListStore.GetContacts(contactList);
        var first = source.IncludedLists.FirstOrDefault();
        var firstIncludedList = first != null ? this.ListStore.FindById(first) : null;

        if (firstIncludedList == null)
        {
          // TODO:[Minor] Remove redundant check.
          if (args.InitialContactList != null)
          {
            var initialSource = args.InitialContactList.Source;
            if (initialSource.IncludedLists.Count > 0 && source.IncludedLists.Count == 0)
            {
              args.RemoveAssociationsContacts = initialContacts;
              return;
            }
          }

          args.AbortPipeline();
          return;
        }

        string initialTagFormat = string.Format("{0}:{1}", Constants.ContactListAssociationTagName, contactList.Id);
        string firstIncludedListTagFormat = string.Format("{0}:{1}", Constants.ContactListAssociationTagName, firstIncludedList.Id);

        Expression<Func<ContactData, bool>> predicate = doc => doc.Tags.Contains(firstIncludedListTagFormat);
        foreach (var includedListId in source.IncludedLists.Skip(1))
        {
          var tagFormat = string.Format("{0}:{1}", Constants.ContactListAssociationTagName, includedListId);
          predicate = PredicateBuilder.Or(predicate, doc => doc.Tags.Contains(tagFormat));
        }
        foreach (var excludedListId in source.ExcludedLists)
        {
          var tagFormat = string.Format("{0}:{1}", Constants.ContactListAssociationTagName, excludedListId);
          predicate = PredicateBuilder.And(predicate, doc => !doc.Tags.Contains(tagFormat));
        }

        args.AddAssociationsContacts =
          searchContext.GetQueryable<ContactData>()
            .Where(PredicateBuilder.And(predicate, doc => !doc.Tags.Contains(initialTagFormat)))
            .ToArray();

        args.RemoveAssociationsContacts =
          searchContext.GetQueryable<ContactData>()
            .Where(PredicateBuilder.And(doc => doc.Tags.Contains(initialTagFormat), PredicateBuilder.Not(predicate)))
            .ToArray();
      }
    }
  }
}