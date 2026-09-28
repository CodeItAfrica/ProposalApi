using GibsLifesMicroWebApi.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Gibs.Domain.Entities
{
    public class SalesChannel
	{
        [Key]
        public string? BranchID { get; private set; }
        public string ChannelID { get; private set; }
        [Required]
        public string ChannelName { get; private set; } = "Channel 1";

        //Navigation Properties
        public virtual Branch? Branch { get; private set; }

        // If ReadOnlyList is a custom class, use ICollection for EF Core navigation
        public virtual ICollection<SalesSubChannel> SubChannels { get; private set; }

        // EF Core requires a parameterless constructor
        protected SalesChannel()
        {
            SubChannels = new List<SalesSubChannel>();
        }

        // Constructor for creating new instances
        public SalesChannel(string channelID, string channelName, string? branchID = null)
        {
            ChannelID = channelID ?? throw new ArgumentNullException(nameof(channelID));
            ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
            BranchID = branchID;
            SubChannels = new List<SalesSubChannel>();
        }

        // Methods to update the entity (following DDD principles)
        public void UpdateChannelName(string channelName)
        {
            ChannelName = channelName ?? throw new ArgumentNullException(nameof(channelName));
        }

        public void UpdateBranchID(string? branchID)
        {
            BranchID = branchID;
        }

        // Methods to manage sub-channels
        public void AddSubChannel(SalesSubChannel subChannel)
        {
            if (subChannel == null) throw new ArgumentNullException(nameof(subChannel));
            SubChannels.Add(subChannel);
        }

        public void RemoveSubChannel(SalesSubChannel subChannel)
        {
            if (subChannel != null)
            {
                SubChannels.Remove(subChannel);
            }
        }

        // Read-only access to sub-channels
        public IReadOnlyList<SalesSubChannel> GetSubChannels()
        {
            return SubChannels.ToList().AsReadOnly();
        }
    }

	public class SalesSubChannel
	{
        [Key]
        public string ChannelID { get; private set; }

		public string SubChannelID { get; private set; }
		[Required]
        public string SubChannelName { get; private set; }

        public string Description { get; private set; }


        //Navigation Properties
        public virtual SalesChannel SalesChannel { get; private set; }
    }

    public class Marketer
	{
		[Key]
        public string MarketerID { get; private set; }
        public string? MarketingUnitID { get; private set; }
        public string? MarketingGroupID { get; private set; }
		[Required]
        public string FullName { get; private set; }

        public DateTime? YearStart { get; private set; }

        public DateTime? YearEnd { get; private set; }

        public decimal? CurrentTarget { get; private set; }

        public decimal? PreviousTarget { get; private set; }

		//Navigation Properties
        public virtual SalesSubChannel Group { get; private set; }
        public virtual SalesChannel Unit { get; private set; }

        protected Marketer() { /*EfCore*/ }

        public Marketer(string marketerID, string fullName, string? marketingUnitID, string? marketingGroupID)
        {
            MarketerID = marketerID;
            MarketingUnitID = marketingUnitID;
            MarketingGroupID = marketingGroupID;
            FullName = fullName;
        }
    }
}
