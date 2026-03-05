using CES.Business.Models;
using CES.Entities;
using CES.Entities.Enums;
using CES.Entities.Infrastructure;
using CES.Entities.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace CES.Business.Extensions.Entities
{
    public static class SubmissionExtensions
    {
        public static Submission ToEntity(this EvidenceSubmissionModel model)
        {
            var entity = new Submission
            {
                DisputantName = model.DisputantName,
                Location = model.Location,
                OfficerNumber = model.OfficerNumber,
                Room = model.Room,
                TicketNumber = model.TicketNumber
            };

            return entity;
        }
    }
}