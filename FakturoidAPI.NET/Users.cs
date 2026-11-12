using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Birko.Fakturoid
{
    // https://www.fakturoid.cz/api/v3/users
    public class Users
    {
        private readonly Fakturoid fakturoid;

        public Users(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // GET /user.json: the user of the credentials with the accounts they can use (UserAccount.Slug is
        // the Fakturoid.AccountSlug). Needs no AccountSlug.
        public Task<Response.User> Current(CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<Response.User>("user.json", cancellationToken);
        }

        // Users of the account (AccountSlug).
        public Task<List<Response.User>> List(CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<List<Response.User>>(fakturoid.AccountPath("users.json"), cancellationToken);
        }
    }

    // https://www.fakturoid.cz/api/v3/account; exposed as Fakturoid.Account.
    public class Accounts
    {
        private readonly Fakturoid fakturoid;

        public Accounts(Fakturoid fakturoid)
        {
            this.fakturoid = fakturoid;
        }

        // Detail of the account (AccountSlug); read-only.
        public Task<Response.Account> View(CancellationToken cancellationToken = default(CancellationToken))
        {
            return fakturoid.Get<Response.Account>(fakturoid.AccountPath("account.json"), cancellationToken);
        }
    }
}
