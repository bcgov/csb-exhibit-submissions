using CES.Business.Models;
using CES.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Interfaces
{
    public interface IFileService
    {
        public Task<StoredFiles?> RetrieveFileMetaData(Guid fileId);
    }
}
