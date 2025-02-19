﻿namespace PureHDF.VOL.Native;

internal class FilterPipelineMessage : Message
{
    #region Fields

    private byte _version;
    private byte _filterCount;

    #endregion

    #region Constructors

    public FilterPipelineMessage(H5DriverBase driver)
    {
        // version
        Version = driver.ReadByte();

        // filter count
        FilterCount = driver.ReadByte();

        // reserved
        if (Version == 1)
            driver.ReadBytes(6);

        // filter descriptions
        FilterDescriptions = new List<FilterDescription>(FilterCount);

        for (int i = 0; i < FilterCount; i++)
        {
            FilterDescriptions.Add(new FilterDescription(driver, Version));
        }
    }

    #endregion

    #region Properties

    public byte Version
    {
        get
        {
            return _version;
        }
        set
        {
            if (!(1 <= value && value <= 2))
                throw new FormatException($"Only version 1 and 2 instances of type {nameof(FilterPipelineMessage)} are supported.");

            _version = value;
        }
    }

    public byte FilterCount
    {
        get
        {
            return _filterCount;
        }
        set
        {
            if (value > 32)
                throw new FormatException($"An instance of type {nameof(FilterPipelineMessage)} can only contain a maximum of 32 filters.");

            _filterCount = value;
        }
    }

    public List<FilterDescription> FilterDescriptions { get; set; }

    #endregion
}