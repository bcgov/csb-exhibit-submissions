using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Entities.Infrastructure
{
    public class BaseEntity
    {
        public int Id { get; set; }

        /// <summary>
        /// FK to <c>ApplicationUser.Id</c>. Null when the row was written by the system rather
        /// than by a signed-in user, or by a session whose local user row could not be resolved.
        /// <para>
        /// Stored as an id rather than a name/email so the audit trail keeps pointing at the same
        /// person after an IDIR rename; the display value is resolved from ApplicationUser on read.
        /// </para>
        /// </summary>
        public int? CreatedByUserId { get; set; }
        public DateTime CreatedDateUTC { get; set; }

        /// <inheritdoc cref="CreatedByUserId"/>
        public int? UpdatedByUserId { get; set; }
        public DateTime? UpdatedDateUTC { get; set; }

        public bool IsDeleted { get; set; }

        public BaseEntity()
        {
            CreatedDateUTC = SystemDate.UtcNow();
            IsDeleted = false;
        }

        /// <param name="updatedByUserId">
        /// The acting user's <c>ApplicationUser.Id</c>, or null for a system-driven update.
        /// </param>
        public void SetUpdateBy(int? updatedByUserId = null)
        {
            UpdatedByUserId = updatedByUserId;
            UpdatedDateUTC = SystemDate.UtcNow();
        }
    }
}
