using System;
using System.Collections.Generic;

namespace GibsLifesMicroWebApi.Models
{
    public class NaicomDetail
    {
        public enum NaicomStatusEnum
        {
            PENDING, SENT, IGNORED, ARCHIVED
        }

        public NaicomStatusEnum? Status { get; private set; }
        public string? UniqueID { get; private set; }
        public DateTime? SubmitDate { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string? JsonPayload { get; private set; }


        protected NaicomDetail() { /*EfCore*/ }

        public static NaicomDetail Success(string uniqueID)
        {
            return new NaicomDetail
            {
                UniqueID = uniqueID,
                Status = NaicomStatusEnum.SENT,
                SubmitDate = DateTime.Now
            };
        }

        public static NaicomDetail FactoryCreate()
        {
            return new NaicomDetail
            {
                Status = NaicomStatusEnum.PENDING,
            };
        }

        public static NaicomDetail FailedTemporary(string errorMessage)
        {
            return new NaicomDetail
            {
                Status = NaicomStatusEnum.IGNORED,
                ErrorMessage = Truncate(errorMessage, 1024),
                //JsonPayload = jsonPayload,
                SubmitDate = DateTime.Now
            };
        }

        public static NaicomDetail FailedPermanent(IList<string> errorMessages, string jsonPayload)
        {
            var errorMessage = string.Join(",", errorMessages);
            return FailedPermanent(errorMessage, jsonPayload);
        }

        public static NaicomDetail FailedPermanent(string errorMessage, string jsonPayload)
        {
            return new NaicomDetail
            {
                Status = NaicomStatusEnum.ARCHIVED,
                ErrorMessage = Truncate(errorMessage, 1024),
                JsonPayload = jsonPayload,
                SubmitDate = DateTime.Now
            };
        }

        private static string Truncate(string errorMessage, int length)
        {
            return errorMessage[..Math.Min(length, errorMessage.Length)];
        }
        //private static string Truncate(IList<string> errorMessages, int length)
        //{
        //    var errors = string.Join(",", errorMessages);
        //    return errors[..Math.Min(length, errors.Length)];
        //}
    }
}