﻿using System;
using ModelingEvolution.Plumberd.EventProcessing;
using ModelingEvolution.Plumberd.EventStore;

namespace ModelingEvolution.Plumberd.GrpcProxy
{
    [ProcessingUnitConfig(IsEventEmitEnabled = true,
        IsCommandEmitEnabled = false,
        IsPersistent = true,
        SubscribesFromBeginning = false,
        ProcessingMode = ProcessingMode.CommandHandler)]
    public class BlobCommandHandler
    {
        public BlobUploaded When(Guid id, UploadBlob cmd)
        {
            return new BlobUploaded()
            {
                Name = cmd.Name,
                Size = cmd.Size,
                StreamCategory = cmd.StreamCategory,
                Reason = cmd.Reason,
                Properties = cmd.Properties
            };
        }
    }
}