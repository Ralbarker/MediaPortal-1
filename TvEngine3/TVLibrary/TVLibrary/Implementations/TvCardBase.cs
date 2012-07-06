#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DirectShowLib;
using DirectShowLib.BDA;
using MediaPortal.Common.Utils;
using TvDatabase;
using TvLibrary.ChannelLinkage;
using TvLibrary.Channels;
using TvLibrary.Epg;
using TvLibrary.Implementations.DVB;
using TvLibrary.Interfaces;
using TvLibrary.Interfaces.Device;

namespace TvLibrary.Implementations
{
  /// <summary>
  /// Base class for all tv cards
  /// </summary>
  public abstract class TvCardBase : ITVCard
  {
    #region events

    /// <summary>
    /// Delegate for the after tune event.
    /// </summary>
    public delegate void OnAfterTuneDelegate();

    /// <summary>
    /// After tune observer event.
    /// </summary>
    public event OnAfterTuneDelegate AfterTuneEvent;

    /// <summary>
    /// Handles the after tune observer event.
    /// </summary>
    protected void TvCardBase_OnAfterTuneEvent()
    {
      if (AfterTuneEvent != null)
      {
        AfterTuneEvent();
      }
    }

    #endregion

    #region variables

    /// <summary>
    /// Indicates if the card should be preloaded.
    /// </summary>
    protected bool _preloadCard;

    /// <summary>
    /// Scanning Paramters
    /// </summary>
    protected ScanParameters _parameters;

    /// <summary>
    /// Dictionary of the corresponding sub channels
    /// </summary>
    protected Dictionary<int, BaseSubChannel> _mapSubChannels;

    /// <summary>
    /// Indicates, if the card is a hybrid one
    /// </summary>
    protected bool _isHybrid;

    /// <summary>
    /// Context reference
    /// </summary>
    protected object m_context;

    /// <summary>
    /// Indicates, if the tuner is locked
    /// </summary>
    protected bool _tunerLocked;

    /// <summary>
    /// Value of the signal level
    /// </summary>
    protected int _signalLevel;

    /// <summary>
    /// Value of the signal quality
    /// </summary>
    protected int _signalQuality;

    /// <summary>
    /// Device Path of the tv card
    /// </summary>
    protected String _devicePath;

    /// <summary>
    /// Indicates, if the card is grabbing epg
    /// </summary>
    protected bool _epgGrabbing;

    /// <summary>
    /// Name of the tv card
    /// </summary>
    protected String _name;

    /// <summary>
    /// Indicates, if the card is scanning
    /// </summary>
    protected bool _isScanning;

    /// <summary>
    /// The graph builder
    /// </summary>
    protected IFilterGraph2 _graphBuilder;

    /// <summary>
    /// Indicates, if the card sub channels
    /// </summary>
    protected bool _supportsSubChannels;

    /// <summary>
    /// The tuner type (eg. DVB-S, DVB-T... etc.).
    /// </summary>
    protected CardType _tunerType;

    /// <summary>
    /// Date and time of the last signal update
    /// </summary>
    protected DateTime _lastSignalUpdate;

    /// <summary>
    /// Last subchannel id
    /// </summary>
    protected int _subChannelId;

    /// <summary>
    /// Indicates, if the signal is present
    /// </summary>
    protected bool _signalPresent;

    /// <summary>
    /// Indicates, if the card is present
    /// </summary>
    protected bool _cardPresent = true;

    /// <summary>
    /// The tuner device
    /// </summary>
    protected DsDevice _tunerDevice;

    /// <summary>
    /// Main device of the card
    /// </summary>
    protected DsDevice _device;

    /// <summary>
    /// The db card id
    /// </summary>
    protected int _cardId;


    /// <summary>
    /// The action that will be taken when a device is no longer being actively used.
    /// </summary>
    protected DeviceIdleMode _idleMode = DeviceIdleMode.Stop;

    /// <summary>
    /// An indicator: has the device been initialised? For most devices this indicates whether the DirectShow/BDA
    /// filter graph has been built.
    /// </summary>
    protected bool _isDeviceInitialised = false;

    /// <summary>
    /// A list containing the custom device interfaces supported by this device. The list is ordered by
    /// interface priority.
    /// </summary>
    protected List<ICustomDevice> _customDeviceInterfaces = null;

    /// <summary>
    /// Enable or disable the use of conditional access interface(s).
    /// </summary>
    protected bool _useConditionalAccessInterace = true;

    /// <summary>
    /// The type of conditional access module available to the conditional access interface.
    /// </summary>
    /// <remarks>
    /// Certain conditional access modules require specific handling to ensure compatibility.
    /// </remarks>
    protected CamType _camType = CamType.Default;

    /// <summary>
    /// The number of channels that the device is capable of or permitted to decrypt simultaneously. Zero means
    /// there is no limit.
    /// </summary>
    protected int _decryptLimit = 0;

    /// <summary>
    /// The method that should be used to communicate the set of channels that the device's conditional access
    /// interface needs to manage.
    /// </summary>
    /// <remarks>
    /// Multi-channel decrypt is *not* the same as Digital Devices' multi-transponder decrypt (MTD). MCD is a
    /// implmented using standard CA PMT commands; MTD is implemented in the Digital Devices drivers.
    /// Disabled = Always send Only. In most cases this will result in only one channel being decrypted. If other
    ///         methods are not working reliably then this one should at least allow decrypting one channel
    ///         reliably.
    /// List = Explicit management using Only, First, More and Last. This is the most widely supported set
    ///         of commands, however they are not suitable for some interfaces (such as the Digital Devices
    ///         interface).
    /// Changes = Use Add, Update and Remove to pass changes to the interface. The full channel list is never
    ///         passed. Most interfaces don't support these commands.
    /// </remarks>
    protected MultiChannelDecryptMode _multiChannelDecryptMode = MultiChannelDecryptMode.List;

    /// <summary>
    /// Enable or disable waiting for the conditional interface to be ready before sending commands.
    /// </summary>
    protected bool _waitUntilCaInterfaceReady = true;

    /// <summary>
    /// The number of times to re-attempt decrypting the current service set when one or more services are
    /// not able to be decrypted for whatever reason.
    /// </summary>
    /// <remarks>
    /// Each available CA interface will be tried in order of priority. If decrypting is not started
    /// successfully, all interfaces are retried until each interface has been tried
    /// _decryptFailureRetryCount + 1 times, or until decrypting is successful.
    /// </remarks>
    protected int _decryptFailureRetryCount = 2;

    /// <summary>
    /// The mode to use for controlling device PID filter(s).
    /// </summary>
    /// <remarks>
    /// This setting can be used to enable or disable the device's PID filter even when the tuning context
    /// (for example, DVB-S vs. DVB-S2) would usually result in different behaviour. Note that it is usually
    /// not ideal to have to manually enable or disable a PID filter as it can affect tuning reliability.
    /// </remarks>
    protected PidFilterMode _pidFilterMode = PidFilterMode.Auto;

    /// <summary>
    /// The previous channel that the device was tuned to. This variable is reset each time the DirectShow
    /// graph is stopped, paused or rebuilt.
    /// </summary>
    protected IChannel _previousChannel = null;

    /// <summary>
    /// Enable or disable the use of custom device interfaces for tuning.
    /// </summary>
    /// <remarks>
    /// Custom/direct tuning may be faster or more reliable than regular tuning methods. Equally, it can
    /// also be slower (eg. TeVii) or more limiting (eg. Digital Everywhere) than regular tuning methods.
    /// </remarks>
    protected bool _useCustomTuning = false;

    #endregion

    #region ctor

    ///<summary>
    /// Base constructor
    ///</summary>
    ///<param name="device">Base DS device</param>
    protected TvCardBase(DsDevice device)
    {
      _isDeviceInitialised = false;
      _mapSubChannels = new Dictionary<int, BaseSubChannel>();
      _lastSignalUpdate = DateTime.MinValue;
      _parameters = new ScanParameters();
      _epgGrabbing = false;   // EPG grabbing not supported by default.
      _customDeviceInterfaces = new List<ICustomDevice>();

      _device = device;
      _tunerDevice = device;
      if (device != null)
      {
        _name = device.Name;
        _devicePath = device.DevicePath;
      }

      if (_devicePath != null)
      {
        TvBusinessLayer layer = new TvBusinessLayer();
        Card c = layer.GetCardByDevicePath(_devicePath);
        if (c != null)
        {
          _cardId = c.IdCard;
          _preloadCard = c.PreloadCard;
          _idleMode = (DeviceIdleMode)c.IdleMode;
          _pidFilterMode = (PidFilterMode)c.PidFilterMode;
          _useCustomTuning = c.UseCustomTuning;

          // Conditional access...
          _useConditionalAccessInterace = c.UseConditionalAccess;
          _camType = (CamType)c.CamType;
          _decryptLimit = c.DecryptLimit;
          _multiChannelDecryptMode = (MultiChannelDecryptMode)c.MultiChannelDecryptMode;
        }
      }
    }

    #endregion

    #region properties

    /// <summary>
    /// Gets or sets the unique id of this card
    /// </summary>
    public virtual int CardId
    {
      get { return _cardId; }
      set { _cardId = value; }
    }


    /// <summary>
    /// returns true if card should be preloaded
    /// </summary>
    public bool PreloadCard
    {
      get { return _preloadCard; }
    }

    /// <summary>
    /// Gets a value indicating whether card supports subchannels
    /// </summary>
    /// <value><c>true</c> if card supports sub channels; otherwise, <c>false</c>.</value>
    public bool SupportsSubChannels
    {
      get { return _supportsSubChannels; }
    }

    /// <summary>
    /// Gets or sets the parameters.
    /// </summary>
    /// <value>A set of timeout and LNB parameters used for tuning and scanning.</value>
    public ScanParameters Parameters
    {
      get { return _parameters; }
      set
      {
        _parameters = value;
        Dictionary<int, BaseSubChannel>.Enumerator en = _mapSubChannels.GetEnumerator();
        while (en.MoveNext())
        {
          en.Current.Value.Parameters = value;
        }
      }
    }

    /// <summary>
    /// Gets/sets the card name
    /// </summary>
    /// <value></value>
    public string Name
    {
      get { return _name; }
      set { _name = value; }
    }

    /// <summary>
    /// returns true if card is currently present
    /// </summary>
    public bool CardPresent
    {
      get { return _cardPresent; }
      set { _cardPresent = value; }
    }

    /// <summary>
    /// Gets/sets the card device
    /// </summary>
    public virtual string DevicePath
    {
      get { return _devicePath; }
    }

    /// <summary>
    /// Gets the device tuner type.
    /// </summary>
    public virtual CardType CardType
    {
      get { return _tunerType; }
    }

    /// <summary>
    /// Returns a <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </summary>
    /// <returns>
    /// A <see cref="T:System.String"></see> that represents the current <see cref="T:System.Object"></see>.
    /// </returns>
    public override string ToString()
    {
      return _name;
    }

    #region tuning range properties

    /// <summary>
    /// Get the minimum channel number that the device is capable of tuning (only applicable for analog
    /// tuners - should be removed if possible).
    /// </summary>
    /// <value>
    /// <c>-1</c> if the property is not applicable, otherwise the minimum channel number that the device
    /// is capable of tuning
    /// </value>
    public int MinChannel
    {
      get
      {
        return -1;
      }
    }

    /// <summary>
    /// Get the maximum channel number that the device is capable of tuning (only applicable for analog
    /// tuners - should be removed if possible).
    /// </summary>
    /// <value>
    /// <c>-1</c> if the property is not applicable, otherwise the maximum channel number that the device
    /// is capable of tuning
    /// </value>
    public int MaxChannel
    {
      get
      {
        return -1;
      }
    }

    #endregion

    #region conditional access properties

    /// <summary>
    /// Get/set the type of conditional access module available to the conditional access interface.
    /// </summary>
    /// <value>The type of the cam.</value>
    public CamType CamType
    {
      get
      {
        return _camType;
      }
      set
      {
        _camType = value;
      }
    }

    /// <summary>
    /// Get the device's conditional access interface decrypt limit. This is usually the number of channels
    /// that the interface is able to decrypt simultaneously. A value of zero indicates that the limit is
    /// to be ignored.
    /// </summary>
    public int DecryptLimit
    {
      get
      {
        return _decryptLimit;
      }
    }

    /// <summary>
    /// Does the device support conditional access?
    /// </summary>
    /// <value><c>true</c> if the device supports conditional access, otherwise <c>false</c></value>
    public bool IsConditionalAccessSupported
    {
      get
      {
        if (!_useConditionalAccessInterace)
        {
          return false;
        }
        // Return true if any interface implements IConditionalAccessProvider.
        foreach (ICustomDevice d in _customDeviceInterfaces)
        {
          if (d is IConditionalAccessProvider)
          {
            return true;
          }
        }
        return false;
      }
    }

    /// <summary>
    /// Get the device's conditional access menu interaction interface. This interface is only applicable if
    /// conditional access is supported.
    /// </summary>
    /// <value><c>null</c> if the device does not support conditional access</value>
    public ICiMenuActions CaMenuInterface
    {
      get
      {
        if (!_useConditionalAccessInterace)
        {
          return null;
        }
        // Return the first interface that implements ICiMenuActions.
        foreach (ICustomDevice d in _customDeviceInterfaces)
        {
          ICiMenuActions caMenuInterface = d as ICiMenuActions;
          if (caMenuInterface != null)
          {
            return caMenuInterface;
          }
        }
        return null;
      }
    }

    /// <summary>
    /// Get a count of the number of services that the device is currently decrypting.
    /// </summary>
    /// <value>The number of services currently being decrypted.</value>
    public int NumberOfChannelsDecrypting
    {
      get
      {
        // If not decrypting any channels or the limit is diabled then return zero.
        if (_mapSubChannels == null || _mapSubChannels.Count == 0 || _decryptLimit == 0)
        {
          return 0;
        }

        HashSet<long> decryptedServices = new HashSet<long>();
        Dictionary<int, BaseSubChannel>.Enumerator en = _mapSubChannels.GetEnumerator();
        while (en.MoveNext())
        {
          IChannel service = en.Current.Value.CurrentChannel;
          DVBBaseChannel digitalService = service as DVBBaseChannel;
          if (digitalService != null)
          {
            if (!decryptedServices.Contains(digitalService.ServiceId))
            {
              decryptedServices.Add(digitalService.ServiceId);
            }
          }
          else
          {
            AnalogChannel analogService = service as AnalogChannel;
            if (analogService != null)
            {
              if (!decryptedServices.Contains(analogService.Frequency))
              {
                decryptedServices.Add(analogService.Frequency);
              }
            }
            else
            {
              throw new TvException("TvCardBase: service type not recognised, unable to count number of services being decrypted\r\n" + service.ToString());
            }
          }
        }

        return decryptedServices.Count;
      }
    }

    #endregion

    /// <summary>
    /// Get the device's DiSEqC control interface. This interface is only applicable for satellite tuners.
    /// It is used for controlling switch, positioner and LNB settings.
    /// </summary>
    /// <value><c>null</c> if the tuner is not a satellite tuner or the tuner does not support sending/receiving
    /// DiSEqC commands</value>
    public virtual IDiseqcController DiseqcController
    {
      get
      {
        return null;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this instance is hybrid.
    /// </summary>
    /// <value><c>true</c> if this instance is hybrid; otherwise, <c>false</c>.</value>
    public bool IsHybrid
    {
      get { return _isHybrid; }
      set { _isHybrid = value; }
    }

    /// <summary>
    /// boolean indicating if tuner is locked to a signal
    /// </summary>
    public virtual bool IsTunerLocked
    {
      get
      {
        UpdateSignalStatus(true);
        return _tunerLocked;
      }
    }

    /// <summary>
    /// returns the signal quality
    /// </summary>
    public int SignalQuality
    {
      get
      {
        UpdateSignalStatus();
        if (_signalQuality < 0)
          _signalQuality = 0;
        if (_signalQuality > 100)
          _signalQuality = 100;
        return _signalQuality;
      }
    }

    /// <summary>
    /// returns the signal level
    /// </summary>
    public int SignalLevel
    {
      get
      {
        UpdateSignalStatus();
        if (_signalLevel < 0)
          _signalLevel = 0;
        if (_signalLevel > 100)
          _signalLevel = 100;
        return _signalLevel;
      }
    }

    /// <summary>
    /// Resets the signal update.
    /// </summary>
    public void ResetSignalUpdate()
    {
      _lastSignalUpdate = DateTime.MinValue;
    }

    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    /// <value>The context.</value>
    public object Context
    {
      get { return m_context; }
      set { m_context = value; }
    }

    /// <summary>
    /// returns true if card is currently grabbing the epg
    /// </summary>
    public bool IsEpgGrabbing
    {
      get { return _epgGrabbing; }
      set
      {
        UpdateEpgGrabber(value);
        _epgGrabbing = value;
      }
    }

    /// <summary>
    /// returns true if card is currently scanning
    /// </summary>
    public bool IsScanning
    {
      get { return _isScanning; }
      set
      {
        _isScanning = value;
      }
    }

    #endregion

    /// <summary>
    /// Checks if the graph is running
    /// </summary>
    /// <returns>true, if the graph is running; false otherwise</returns>
    protected bool GraphRunning()
    {
      bool graphRunning = false;

      if (_graphBuilder != null)
      {
        FilterState state;
        ((IMediaControl)_graphBuilder).GetState(10, out state);
        graphRunning = (state == FilterState.Running);
      }
      //Log.Log.WriteFile("subch:{0} GraphRunning: {1}", _subChannelId, graphRunning);
      return graphRunning;
    }

    /// <summary>
    /// Load the <see cref="T:TvLibrary.Interfaces.ICustomDevice"/> plugins for this device.
    /// </summary>
    /// <remarks>
    /// It is expected that this function will be called at some stage during the DirectShow graph building process.
    /// This function may update the lastFilter reference parameter to insert filters for IAddOnDevice
    /// plugins.
    /// </remarks>
    /// <param name="mainFilter">The main device source filter. Usually a tuner filter.</param>
    /// <param name="lastFilter">The source filter (usually either a tuner or capture/receiver filter) to
    ///   connect the [first] device filter to.</param>
    protected void LoadPlugins(IBaseFilter mainFilter, ref IBaseFilter lastFilter)
    {
      Log.Log.Debug("TvCardBase: load custom device plugins");

      if (mainFilter == null)
      {
        Log.Log.Debug("TvCardBase: the main filter is null");
        return;
      }
      if (!Directory.Exists("plugins") || !Directory.Exists("plugins\\CustomDevices"))
      {
        Log.Log.Debug("TvCardBase: plugin directory doesn't exist or is not accessible");
        return;
      }

      // Load all available and compatible plugins.
      List<ICustomDevice> plugins = new List<ICustomDevice>();
      String[] dllNames = Directory.GetFiles("plugins\\CustomDevices", "*.dll");
      foreach (String dllName in dllNames)
      {
        Assembly dll = Assembly.LoadFrom(dllName);
        Type[] pluginTypes = dll.GetExportedTypes();
        foreach (Type type in pluginTypes)
        {
          if (type.IsClass && !type.IsAbstract)
          {
            Type cdInterface = type.GetInterface("ICustomDevice");
            if (cdInterface != null)
            {
              if (CompatibilityManager.IsPluginCompatible(type))
              {
                ICustomDevice plugin = (ICustomDevice)Activator.CreateInstance(type);
                plugins.Add(plugin);
              }
              else
              {
                Log.Log.Debug("TvCardBase: skipping incompatible plugin \"{0}\" ({1})", type.Name, dllName);
              }
            }
          }
        }
      }

      // There is a well defined loading/checking order for plugins: add-ons, priority, name.
      plugins.Sort(
        delegate(ICustomDevice cd1, ICustomDevice cd2)
        {
          bool cd1IsAddOn = cd1 is IAddOnDevice;
          bool cd2IsAddOn = cd2 is IAddOnDevice;
          if (cd1IsAddOn && !cd2IsAddOn)
          {
            return -1;
          }
          if (cd2IsAddOn && !cd1IsAddOn)
          {
            return 1;
          }
          int priorityCompare = cd2.Priority.CompareTo(cd1.Priority);
          if (priorityCompare != 0)
          {
            return priorityCompare;
          }
          return cd1.Name.CompareTo(cd2.Name);
        }
      );

      // Log the name, priority and capabilities for each plugin, in priority order.
      foreach (ICustomDevice d in plugins)
      {
        Type[] interfaces = d.GetType().GetInterfaces();
        String[] interfaceNames = new String[interfaces.Length];
        for (int i = 0; i < interfaces.Length; i++)
        {
          interfaceNames[i] = interfaces[i].Name;
        }
        Array.Sort(interfaceNames);
        Log.Log.Debug("  {0} [{1} - {2}]: {3}", d.Name, d.Priority, d.GetType().Name, String.Join(", ", interfaceNames));
      }

      Log.Log.Debug("TvCardBase: checking for supported plugins");
      _customDeviceInterfaces = new List<ICustomDevice>();
      foreach (ICustomDevice d in plugins)
      {
        if (!d.Initialise(mainFilter, _tunerType, _devicePath))
        {
          d.Dispose();
          continue;
        }

        // The plugin is supported. If the plugin is an add on plugin, we attempt to add it to the graph.
        bool isAddOn = false;
        if (lastFilter != null)
        {
          IAddOnDevice addOn = d as IAddOnDevice;
          if (addOn != null)
          {
            Log.Log.Debug("TvCardBase: add-on plugin found");
            if (!addOn.AddToGraph(ref lastFilter))
            {
              Log.Log.Debug("TvCardBase: failed to add device filters to graph");
              addOn.Dispose();
              continue;
            }
            isAddOn = true;
          }
        }

        try
        {
          // When we find the main plugin, then we stop searching...
          if (!isAddOn)
          {
            Log.Log.Debug("TvCardBase: primary plugin found");
            break;
          }
        }
        finally
        {
          _customDeviceInterfaces.Add(d);
        }
      }
      if (_customDeviceInterfaces.Count == 0)
      {
        Log.Log.Debug("TvCardBase: no plugins supported");
      }
    }

    /// <summary>
    /// Open any <see cref="T:TvLibrary.Interfaces.ICustomDevice"/> plugins loaded for this device by LoadPlugins().
    /// </summary>
    /// <remarks>
    /// We separate this from the loading because some plugins (for example, the NetUP plugin) can't be opened
    /// until the graph has finished being built.
    /// </remarks>
    protected void OpenPlugins()
    {
      Log.Log.Debug("TvCardBase: open custom device plugins");
      if (_useConditionalAccessInterace)
      {
        foreach (ICustomDevice plugin in _customDeviceInterfaces)
        {
          IConditionalAccessProvider caProvider = plugin as IConditionalAccessProvider;
          if (caProvider != null)
          {
            caProvider.OpenInterface();
          }
        }
      }
    }

    /// <summary>
    /// Configure the device's PID filter(s) to enable receiving the PIDs for each of the current subchannels.
    /// </summary>
    protected void ConfigurePidFilter()
    {
      Log.Log.Debug("TvCardBase: configure PID filter, mode = {0}", _pidFilterMode);

      if (_tunerType == CardType.Analog || _tunerType == CardType.RadioWebStream || _tunerType == CardType.Unknown)
      {
        Log.Log.Debug("TvCardBase: unsupported device type {0}", _tunerType);
        return;
      }
      if (_mapSubChannels == null || _mapSubChannels.Count == 0)
      {
        Log.Log.Debug("TvCardBase: no subchannels");
        return;
      }

      HashSet<UInt16> pidSet = null;
      ModulationType modulation = ModulationType.ModNotDefined;
      bool checkedModulation = false;
      foreach (ICustomDevice d in _customDeviceInterfaces)
      {
        IPidFilterController filter = d as IPidFilterController;
        if (filter != null)
        {
          Log.Log.Debug("TvCardBase: found PID filter controller interface");

          if (_pidFilterMode == PidFilterMode.Disabled)
          {
            filter.SetFilterPids(null, modulation, false);
            continue;
          }

          if (pidSet == null)
          {
            Log.Log.Debug("TvCardBase: assembling PID list");
            pidSet = new HashSet<UInt16>();
            int count = 1;
            foreach (ITvSubChannel subchannel in _mapSubChannels.Values)
            {
              TvDvbChannel dvbChannel = subchannel as TvDvbChannel;
              if (dvbChannel != null && dvbChannel.Pids != null)
              {
                // Figure out the multiplex modulation scheme.
                if (!checkedModulation)
                {
                  checkedModulation = true;
                  ATSCChannel atscChannel = dvbChannel.CurrentChannel as ATSCChannel;
                  if (atscChannel != null)
                  {
                    modulation = atscChannel.ModulationType;
                  }
                  else
                  {
                    DVBSChannel dvbsChannel = dvbChannel.CurrentChannel as DVBSChannel;
                    if (dvbChannel != null)
                    {
                      modulation = dvbsChannel.ModulationType;
                    }
                    else
                    {
                      DVBCChannel dvbcChannel = dvbChannel.CurrentChannel as DVBCChannel;
                      if (dvbcChannel != null)
                      {
                        modulation = dvbcChannel.ModulationType;
                      }
                    }
                  }
                }

                // Build a distinct super-set of PIDs used by the subchannels.
                foreach (UInt16 pid in dvbChannel.Pids)
                {
                  if (!pidSet.Contains(pid))
                  {
                    Log.Log.Debug("  {0,-2} = {1} (0x{1:x})", count++, pid);
                    pidSet.Add(pid);
                  }
                }
              }
            }
          }
          filter.SetFilterPids(pidSet, modulation, _pidFilterMode == PidFilterMode.Enabled);
        }
      }
    }

    /// <summary>
    /// Update the list of services being decrypted by the device's conditional access interfaces(s).
    /// </summary>
    /// <param name="subChannelId">The ID of the subchannel causing this update.</param>
    /// <param name="updateAction"><c>Add</c> if the subchannel is being tuned, <c>update</c> if the PMT for the
    ///   subchannel has changed, or <c>last</c> if the subchannel is being disposed.</param>
    protected void UpdateDecryptList(int subChannelId, CaPmtListManagementAction updateAction)
    {
      Log.Log.Debug("TvCardBase: subchannel {0} update decrypt list, mode = {1}, update action = {2}", subChannelId, _multiChannelDecryptMode, updateAction);

      if (_mapSubChannels == null || _mapSubChannels.Count == 0 || !_mapSubChannels.ContainsKey(subChannelId))
      {
        Log.Log.Debug("TvCardBase: subchannel not found");
        return;
      }
      if (_mapSubChannels[subChannelId].CurrentChannel.FreeToAir)
      {
        Log.Log.Debug("TvCardBase: service is not encrypted");
        return;
      }

      // First build a distinct list of the services that we need to handle.
      Log.Log.Debug("TvCardBase: assembling service list");
      List<BaseSubChannel> distinctServices = new List<BaseSubChannel>();
      if (_multiChannelDecryptMode == MultiChannelDecryptMode.Disabled || _multiChannelDecryptMode == MultiChannelDecryptMode.Changes)
      {
        // We only send one command relating to the service associated with the subchannel.
        distinctServices.Add(_mapSubChannels[subChannelId]);
      }
      else
      {
        // We send one command for each service that still needs to be decrypted.
        Dictionary<int, BaseSubChannel>.ValueCollection.Enumerator en = _mapSubChannels.Values.GetEnumerator();
        while (en.MoveNext())
        {
          IChannel service = en.Current.CurrentChannel;
          // We don't need to decrypt free-to-air channels.
          if (service.FreeToAir)
          {
            continue;
          }

          bool exists = false;
          foreach (BaseSubChannel serviceToDecrypt in distinctServices)
          {
            DVBBaseChannel digitalService = service as DVBBaseChannel;
            if (digitalService != null)
            {
              if (digitalService.ServiceId == ((DVBBaseChannel)serviceToDecrypt.CurrentChannel).ServiceId)
              {
                exists = true;
                break;
              }
            }
            else
            {
              AnalogChannel analogService = service as AnalogChannel;
              if (analogService != null)
              {
                if (analogService.Frequency == ((AnalogChannel)serviceToDecrypt.CurrentChannel).Frequency &&
                  analogService.ChannelNumber == ((AnalogChannel)serviceToDecrypt.CurrentChannel).ChannelNumber)
                {
                  exists = true;
                  break;
                }
              }
              else
              {
                throw new TvException("TvCardBase: service type not recognised, unable to assemble decrypt service list\r\n" + service.ToString());
              }
            }
          }
          // If this service is the service that is causing this update and the action is "last" (meaning "remove"
          // or "no need to decrypt") then don't add the service to the list because we don't need to keep decrypting
          // it... at least not for this subchannel.
          if (!exists && (subChannelId != en.Current.SubChannelId || updateAction != CaPmtListManagementAction.Last))
          {
            distinctServices.Add(en.Current);
          }
        }
      }

      // This should never happen, regardless of the action that is being performed. Note that this is just a
      // sanity check. It is expected that the service will manage decrypt limit logic. This check does not work
      // for "changes" mode.
      if (_decryptLimit > 0 && distinctServices.Count > _decryptLimit)
      {
        Log.Log.Debug("TvCardBase: decrypt limit exceeded");
        return;
      }
      if (distinctServices.Count == 0)
      {
        Log.Log.Debug("TvCardBase: no services to update");
        return;
      }

      // Identify the conditional access interface(s) and send the service list.
      bool foundCaProvider = false;
      for (int attempt = 1; attempt <= _decryptFailureRetryCount + 1; attempt++)
      {
        if (attempt > 1)
        {
          Log.Log.Debug("TvCardBase: attempt {0}...", attempt);
        }

        foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
        {
          IConditionalAccessProvider caProvider = deviceInterface as IConditionalAccessProvider;
          if (caProvider == null)
          {
            continue;
          }

          Log.Log.Debug("TvCardBase: CA provider {0}...", caProvider.Name);
          foundCaProvider = true;

          if (_waitUntilCaInterfaceReady && !caProvider.IsInterfaceReady())
          {
            Log.Log.Debug("TvCardBase: provider is not ready, waiting for up to 15 seconds", caProvider.Name);
            DateTime startWait = DateTime.Now;
            TimeSpan waitTime = new TimeSpan(0);
            while (waitTime.TotalMilliseconds < 15000)
            {
              System.Threading.Thread.Sleep(200);
              waitTime = DateTime.Now - startWait;
              if (caProvider.IsInterfaceReady())
              {
                Log.Log.Debug("TvCardBase: provider ready after {0} ms", waitTime.TotalMilliseconds);
                break;
              }
            }
          }

          // Ready or not, we send commands now.
          Log.Log.Debug("TvCardBase: sending command(s)");
          bool success = true;
          TvDvbChannel digitalService;
          // The default action is "more" - this will be changed below if necessary.
          CaPmtListManagementAction action = CaPmtListManagementAction.More;

          // The command is "start/continue descrambling" unless we're removing services.
          CaPmtCommand command = CaPmtCommand.OkDescrambling;
          if (updateAction == CaPmtListManagementAction.Last)
          {
            command = CaPmtCommand.NotSelected;
          }
          for (int i = 0; i < distinctServices.Count; i++)
          {
            if (i == 0)
            {
              if (distinctServices.Count == 1)
              {
                if (_multiChannelDecryptMode == MultiChannelDecryptMode.Changes)
                {
                  // Remove a service...
                  if (updateAction == CaPmtListManagementAction.Last)
                  {
                    action = CaPmtListManagementAction.Only;
                  }
                  // Add or update a service...
                  else
                  {
                    action = updateAction;
                  }
                }
                else
                {
                  action = CaPmtListManagementAction.Only;
                }
              }
              else
              {
                action = CaPmtListManagementAction.First;
              }
            }
            else if (i == distinctServices.Count - 1)
            {
              action = CaPmtListManagementAction.Last;
            }
            else
            {
              action = CaPmtListManagementAction.More;
            }

            Log.Log.Debug("  command = {0}, action = {1}, service = {2}", command, action, distinctServices[i].CurrentChannel.Name);
            digitalService = distinctServices[i] as TvDvbChannel;
            if (digitalService == null)
            {
              success &= caProvider.SendCommand(distinctServices[i].CurrentChannel, action, command, null, null);
            }
            else
            {
              success &= caProvider.SendCommand(distinctServices[i].CurrentChannel, action, command, digitalService.Pmt, digitalService.Cat);
            }
          }

          // Are we done?
          if (success)
          {
            return;
          }
        }

        if (!foundCaProvider)
        {
          Log.Log.Debug("TvCardBase: no CA providers identified");
          return;
        }
      }
    }

    #region HelperMethods

    /// <summary>
    /// Gets the first subchannel being used.
    /// </summary>
    /// <value>The current channel.</value>
    private int firstSubchannel
    {
      get
      {
        foreach (int i in _mapSubChannels.Keys)
        {
          if (_mapSubChannels.ContainsKey(i))
          {
            return i;
          }
        }
        return 0;
      }
    }

    /// <summary>
    /// Gets or sets the current channel.
    /// </summary>
    /// <value>The current channel.</value>
    public IChannel CurrentChannel
    {
      get
      {
        if (_mapSubChannels.Count > 0)
        {
          return _mapSubChannels[firstSubchannel].CurrentChannel;
        }
        return null;
      }
      set
      {
        if (_mapSubChannels.Count > 0)
        {
          _mapSubChannels[firstSubchannel].CurrentChannel = value;
        }
      }
    }

    #endregion

    #region virtual methods

    /// <summary>
    /// Builds the graph.
    /// </summary>
    public virtual void BuildGraph() {}

    /// <summary>
    /// Check if the tuner has acquired signal lock.
    /// </summary>
    /// <returns><c>true</c> if the tuner has locked in on signal, otherwise <c>false</c></returns>
    public virtual bool LockedInOnSignal()
    {
      Log.Log.Debug("TvCardBase: check for signal lock");
      _tunerLocked = false;
      DateTime timeStart = DateTime.Now;
      TimeSpan ts = timeStart - timeStart;
      while (!_tunerLocked && ts.TotalSeconds < _parameters.TimeOutTune)
      {
        UpdateSignalStatus(true);
        if (!_tunerLocked)
        {
          ts = DateTime.Now - timeStart;
          Log.Log.Debug("  waiting 20ms");
          System.Threading.Thread.Sleep(20);
        }
      }

      if (!_tunerLocked)
      {
        Log.Log.Debug("TvCardBase: failed to lock signal");
      }
      else
      {
        Log.Log.Debug("TvCardBase: locked");
      }
      return _tunerLocked;
    }

    /// <summary>
    /// Reload the device configuration.
    /// </summary>
    public virtual void ReloadCardConfiguration()
    {
    }

    /// <summary>
    /// Get the device's channel scanning interface.
    /// </summary>
    public virtual ITVScanning ScanningInterface
    {
      get
      {
        return null;
      }
    }

    #endregion

    #region abstract methods

    /// <summary>
    /// Update the tuner signal status statistics.
    /// </summary>
    protected virtual void UpdateSignalStatus()
    {
      UpdateSignalStatus(false);
    }

    /// <summary>
    /// Update the tuner signal status statistics.
    /// </summary>
    /// <param name="force"><c>True</c> to force the status to be updated (status information may be cached).</param>
    protected abstract void UpdateSignalStatus(bool force);

    /// <summary>
    /// Stop the device. The actual result of this function depends on device configuration.
    /// </summary>
    public virtual void Stop()
    {
      Log.Log.Debug("TvCardBase: stop, idle mode = {0}", _idleMode);
      try
      {
        UpdateEpgGrabber(false);  // Stop grabbing EPG.
        _isScanning = false;
        FreeAllSubChannels();

        DeviceAction action = DeviceAction.Stop;
        switch (_idleMode)
        {
          case DeviceIdleMode.Pause:
            action = DeviceAction.Pause;
            break;
          case DeviceIdleMode.Unload:
            action = DeviceAction.Unload;
            break;
          case DeviceIdleMode.AlwaysOn:
            action = DeviceAction.Start;
            break;
        }

        // Plugins may want to prevent or direct actions to ensure compatibility and smooth device operation.
        DeviceAction pluginAction = action;
        foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
        {
          deviceInterface.OnStop(this, ref pluginAction);
          if (pluginAction > action)
          {
            Log.Log.Debug("TvCardBase: plugin \"{0}\" overrides action {1} with {2}", deviceInterface.Name, action, pluginAction);
            action = pluginAction;
          }
          else if (action != pluginAction)
          {
            Log.Log.Debug("TvCardBase: plugin \"{0}\" wants to perform action {1}, overriden", deviceInterface.Name, pluginAction);
          }
        }

        PerformDeviceAction(action);

        // Turn off the device power.
        foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
        {
          IPowerDevice powerDevice = deviceInterface as IPowerDevice;
          if (powerDevice != null)
          {
            powerDevice.SetPowerState(false);
          }
        }
      }
      finally
      {
        // One potential reason for getting here is that signal could not be locked, and the reason for
        // that may be that tuning failed. We always want to force a full retune on the next tune request
        // in this situation.
        _previousChannel = null;
      }
    }

    /// <summary>
    /// Perform a specific device action. For example, stop the device.
    /// </summary>
    /// <param name="action">The action to perform with the device.</param>
    protected virtual void PerformDeviceAction(DeviceAction action)
    {
      Log.Log.Debug("TvCardBase: perform device action, action = {0}", action);
      try
      {
        if (action == DeviceAction.Reset)
        {
          // TODO: this should work, but it would be better to have Dispose() as final and Decompose() or
          // some other alternative for resetting.
          Dispose();
          BuildGraph();
        }
        else if (action == DeviceAction.Unload)
        {
          Dispose();
        }
        else
        {
          if (_graphBuilder == null)
          {
            Log.Log.Debug("TvCardBase: graphbuilder is null");
            return;
          }

          if (action == DeviceAction.Pause)
          {
            SetGraphState(FilterState.Paused);
          }
          else if (action == DeviceAction.Stop)
          {
            SetGraphState(FilterState.Stopped);
          }
          else if (action == DeviceAction.Start)
          {
            SetGraphState(FilterState.Running);
          }
          else if (action == DeviceAction.Restart)
          {
            SetGraphState(FilterState.Stopped);
            SetGraphState(FilterState.Running);
          }
          else
          {
            Log.Log.Debug("TvCardBase: unhandled action");
            return;
          }
        }

        Log.Log.Debug("TvCardBase: action succeeded");
      }
      catch (Exception ex)
      {
        Log.Log.Debug("TvCardBase: action failed\r\n" + ex.ToString());
      }
    }

    /// <summary>
    /// Set the state of the DirectShow/BDA filter graph.
    /// </summary>
    /// <param name="state">The state to put the filter graph in.</param>
    protected virtual void SetGraphState(FilterState state)
    {
      Log.Log.Debug("TvCardBase: set graph state, state = {0}", state);

      // Get current state.
      FilterState currentState;
      ((IMediaControl)_graphBuilder).GetState(10, out currentState);
      Log.Log.Debug("  current state = {0}", currentState);
      if (state == currentState)
      {
        Log.Log.Debug("TvCardBase: graph already in required state");
        return;
      }
      int hr = 0;
      if (state == FilterState.Stopped)
      {
        hr = ((IMediaControl)_graphBuilder).Stop();
      }
      else if (state == FilterState.Paused)
      {
        hr = ((IMediaControl)_graphBuilder).Pause();
      }
      else
      {
        hr = ((IMediaControl)_graphBuilder).Run();
      }
      if (hr < 0 || hr > 1)
      {
        Log.Log.Error("TvCardBase: failed to perform action, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        throw new TvException("TvCardBase: failed to set graph state");
      }
    }

    /// <summary>
    /// A derrived class may activate / deactivate the epg grabber
    /// </summary>
    /// <param name="value">Mode</param>
    protected virtual void UpdateEpgGrabber(bool value)
    {
    }

    #endregion

    #region scan/tune

    /// <summary>
    /// Check if the tuner can tune to a specific channel.
    /// </summary>
    /// <param name="channel">The channel to check.</param>
    /// <returns><c>true</c> if the tuner can tune to the channel, otherwise <c>false</c></returns>
    public abstract bool CanTune(IChannel channel);

    /// <summary>
    /// Scan a specific channel.
    /// </summary>
    /// <param name="subChannelId">The subchannel ID for the channel that is being scanned.</param>
    /// <param name="channel">The channel to scan.</param>
    /// <returns>the subchannel associated with the scanned channel</returns>
    public virtual ITvSubChannel Scan(int subChannelId, IChannel channel)
    {
      return Tune(subChannelId, channel);
    }

    /// <summary>
    /// Tune to a specific channel.
    /// </summary>
    /// <param name="subChannelId">The subchannel ID for the channel that is being tuned.</param>
    /// <param name="channel">The channel to tune to.</param>
    /// <returns>the subchannel associated with the tuned channel</returns>
    public virtual ITvSubChannel Tune(int subChannelId, IChannel channel)
    {
      Log.Log.Debug("TvCardBase: tune channel, {0}", channel);
      bool newSubChannel = false;
      try
      {
        // The DirectShow/BDA graph needs to be assembled before the channel can be tuned.
        if (!_isDeviceInitialised)
        {
          BuildGraph();
        }

        // Get a subchannel for the service.
        if (!_mapSubChannels.ContainsKey(subChannelId))
        {
          Log.Log.Debug("TvCardBase: creating new subchannel");
          newSubChannel = true;
          subChannelId = CreateNewSubChannel(channel);
        }
        else
        {
          Log.Log.Debug("TvCardBase: using existing subchannel");
          // If reusing a subchannel and our multi-channel decrypt mode is "changes", tell the plugin to stop
          // decrypting the previous service before we lose access to the PMT and CAT.
          if (_multiChannelDecryptMode == MultiChannelDecryptMode.Changes)
          {
            UpdateDecryptList(subChannelId, CaPmtListManagementAction.Last);
          }
        }
        Log.Log.Info("TvCardBase: subchannel ID = {0}, subchannel count = {1}", subChannelId, _mapSubChannels.Count);
        _mapSubChannels[subChannelId].CurrentChannel = channel;

        // Subchannel OnBeforeTune().
        _mapSubChannels[subChannelId].OnBeforeTune();

        // Do we need to tune?
        if (_previousChannel == null || _previousChannel.IsDifferentTransponder(channel))
        {
          // Stop the EPG grabber. We're going to move to a different channel so any EPG data that has been
          // grabbed for the previous channel should be stored.
          UpdateEpgGrabber(false);

          // When we call ICustomDevice.OnBeforeTune(), the ICustomDevice may modify the tuning parameters.
          // However, the original channel object *must not* be modified otherwise IsDifferentTransponder()
          // will sometimes returns true when it shouldn't. See mantis 0002979.
          IChannel tuneChannel = (IChannel)channel.Clone();

          // Plugin OnBeforeTune().
          DeviceAction action = DeviceAction.Default;
          foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
          {
            DeviceAction pluginAction;
            deviceInterface.OnBeforeTune(this, _previousChannel, ref tuneChannel, out pluginAction);
            if (pluginAction != DeviceAction.Unload && pluginAction != DeviceAction.Default)
            {
              // Valid action requested...
              if (pluginAction > action)
              {
                Log.Log.Debug("TvCardBase: plugin \"{0}\" overrides action {1} with {2}", deviceInterface.Name, action, pluginAction);
                action = pluginAction;
              }
              else if (pluginAction != action)
              {
                Log.Log.Debug("TvCardBase: plugin \"{0}\" wants to perform action {1}, overriden", deviceInterface.Name, pluginAction);
              }
            }

            // Turn on device power. This usually needs to happen before tuning.
            IPowerDevice powerDevice = deviceInterface as IPowerDevice;
            if (powerDevice != null)
            {
              powerDevice.SetPowerState(true);
            }
          }
          if (action != DeviceAction.Default)
          {
            PerformDeviceAction(action);
          }

          // Perform tuning.
          PerformTuning(tuneChannel);

          // Plugin OnAfterTune().
          foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
          {
            deviceInterface.OnAfterTune(this, channel);
          }
        }

        // Subchannel OnAfterTune().
        _lastSignalUpdate = DateTime.MinValue;
        _mapSubChannels[subChannelId].OnAfterTune();
      }
      catch (Exception ex)
      {
        Log.Log.Debug("TvCardBase: tuning failed\r\n{0}", ex.ToString());
        if (newSubChannel)
        {
          Log.Log.Debug("TvCardBase: removing subchannel {0}", subChannelId);
          _mapSubChannels.Remove(subChannelId);
          // analog had: FreeSubChannel(subChannel.SubChannelId);
        }
        // We always want to force a retune on the next tune request in this situation.
        _previousChannel = null;
        throw;
      }

      _previousChannel = channel;
      try
      {
        // Start the DirectShow/BDA graph if it is not already running.
        SetGraphState(FilterState.Running);

        // Ensure that data/streams which are required to detect the service will pass through the device's
        // PID filter.
        ConfigurePidFilter();

        // Plugin OnRunning().
        foreach (ICustomDevice deviceInterface in _customDeviceInterfaces)
        {
          deviceInterface.OnRunning(this, _mapSubChannels[subChannelId].CurrentChannel);
        }

        // Check signal lock.
        if (!LockedInOnSignal())
        {
          throw new TvExceptionNoSignal("TvCardBase: failed to lock in on signal");
        }

        // Subchannel OnGraphRunning().
        _mapSubChannels[subChannelId].AfterTuneEvent -= new BaseSubChannel.OnAfterTuneDelegate(TvCardBase_OnAfterTuneEvent);
        _mapSubChannels[subChannelId].AfterTuneEvent += new BaseSubChannel.OnAfterTuneDelegate(TvCardBase_OnAfterTuneEvent);
        _mapSubChannels[subChannelId].OnGraphRunning();
      }
      catch (Exception)
      {
        // One potential reason for getting here is that signal could not be locked, and the reason for
        // that may be that tuning failed. We always want to force a retune on the next tune request in
        // this situation.
        _previousChannel = null;
        if (_mapSubChannels[subChannelId] != null)
        {
          FreeSubChannel(subChannelId);
        }
        throw;
      }

      // At this point we should know which data/streams form the service(s) that are being accessed. We need to
      // ensure those streams will pass through the device's PID filter.
      ConfigurePidFilter();

      // If the service is encrypted, start decrypting it.
      UpdateDecryptList(subChannelId, CaPmtListManagementAction.Add);

      return _mapSubChannels[subChannelId];
    }

    /// <summary>
    /// Actually tune to a channel.
    /// </summary>
    /// <param name="channel">The channel to tune to.</param>
    protected abstract void PerformTuning(IChannel channel);

    /// <summary>
    /// Allocate a new subchannel instance.
    /// </summary>
    /// <param name="channel">The service or channel to associate with the subchannel.</param>
    /// <returns>a handle for the subchannel</returns>
    protected abstract int CreateNewSubChannel(IChannel channel);

    #endregion

    #region quality control

    /// <summary>
    /// Check if the device supports stream quality control.
    /// </summary>
    /// <value></value>
    public virtual bool SupportsQualityControl
    {
      get
      {
        return false;
      }
    }

    /// <summary>
    /// Get the device's quality control interface.
    /// </summary>
    public virtual IQuality Quality
    {
      get
      {
        return null;
      }
    }

    #endregion

    #region EPG

    /// <summary>
    /// Get the device's ITVEPG interface, used for grabbing electronic program guide data.
    /// </summary>
    public virtual ITVEPG EpgInterface
    {
      get
      {
        return null;
      }
    }

    /// <summary>
    /// Abort grabbing electronic program guide data.
    /// </summary>
    public virtual void AbortGrabbing()
    {
    }

    /// <summary>
    /// Get the electronic program guide data found in a grab session.
    /// </summary>
    /// <value>EPG data if the device supports EPG grabbing and grabbing is complete, otherwise <c>null</c></value>
    public virtual List<EpgChannel> Epg
    {
      get
      {
        return null;
      }
    }

    /// <summary>
    /// Start grabbing electronic program guide data (idle EPG grabber).
    /// </summary>
    /// <param name="callback">The delegate to call when grabbing is complete or canceled.</param>
    public virtual void GrabEpg(BaseEpgGrabber callback)
    {
    }

    /// <summary>
    /// Start grabbing electronic program guide data (timeshifting/recording EPG grabber).
    /// </summary>
    public virtual void GrabEpg()
    {
    }

    #endregion

    #region channel linkages

    /// <summary>
    /// Starts scanning for linkages.
    /// </summary>
    /// <param name="callback">The delegate to call when scanning is complete or canceled.</param>
    public virtual void StartLinkageScanner(BaseChannelLinkageScanner callback)
    {
    }

    /// <summary>
    /// Stop/reset the linkage scanner.
    /// </summary>
    public virtual void ResetLinkageScanner()
    {
    }

    /// <summary>
    /// Get the portal channels found by the linkage scanner.
    /// </summary>
    public virtual List<PortalChannel> ChannelLinkages
    {
      get
      {
        return null;
      }
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Close interfaces, free memory and release COM object references.
    /// </summary>
    public virtual void Dispose()
    {
      // Dispose plugins.
      if (_customDeviceInterfaces != null)
      {
        foreach (ICustomDevice device in _customDeviceInterfaces)
        {
          device.Dispose();
        }
      }
      _customDeviceInterfaces = new List<ICustomDevice>();
    }

    #endregion


    #region subchannel management

    /// <summary>
    /// Frees the sub channel.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="subchannelBusy">is the subcannel busy with other users.</param>
    public virtual void FreeSubChannelContinueGraph(int id, bool subchannelBusy)
    {
      FreeSubChannel(id, true);
    }

    /// <summary>
    /// Frees the sub channel. but keeps the graph running.
    /// </summary>
    /// <param name="id">Handle to the subchannel.</param>
    public virtual void FreeSubChannelContinueGraph(int id)
    {
      FreeSubChannel(id, true);
    }

    /// <summary>
    /// Frees the sub channel.
    /// </summary>
    /// <param name="id">Handle to the subchannel.</param>
    public virtual void FreeSubChannel(int id)
    {
      FreeSubChannel(id, false);
    }

    /// <summary>
    /// Frees the sub channel.
    /// </summary>
    /// <param name="id">Handle to the subchannel.</param>
    /// <param name="continueGraph">Indicates, if the graph should be continued or stopped</param>
    private void FreeSubChannel(int id, bool continueGraph)
    {
      Log.Log.Info("tvcard:FreeSubChannel: subchannels count {0} subch#{1} keep graph={2}", _mapSubChannels.Count, id,
                   continueGraph);
      if (_mapSubChannels.ContainsKey(id))
      {
        if (_mapSubChannels[id].IsTimeShifting)
        {
          Log.Log.Info("tvcard:FreeSubChannel :{0} - is timeshifting (skipped)", id);
          return;
        }

        if (_mapSubChannels[id].IsRecording)
        {
          Log.Log.Info("tvcard:FreeSubChannel :{0} - is recording (skipped)", id);
          return;
        }

        try
        {
          UpdateDecryptList(id, CaPmtListManagementAction.Last);
          ConfigurePidFilter();
          _mapSubChannels[id].Decompose();
        }
        finally
        {
          _mapSubChannels.Remove(id);
        }
      }
      else
      {
        Log.Log.Info("tvcard:FreeSubChannel :{0} - sub channel not found", id);
      }
      if (_mapSubChannels.Count == 0)
      {
        _subChannelId = 0;
        if (!continueGraph)
        {
          Log.Log.Info("tvcard:FreeSubChannel : no subchannels present, stopping device");
          Stop();
        }
        else
        {
          Log.Log.Info("tvcard:FreeSubChannel : no subchannels present, continuing graph");
        }
      }
      else
      {
        Log.Log.Info("tvcard:FreeSubChannel : subchannels STILL present {}, continuing graph", _mapSubChannels.Count);
      }
    }

    /// <summary>
    /// Frees all sub channels.
    /// </summary>
    protected void FreeAllSubChannels()
    {
      Log.Log.Info("tvcard:FreeAllSubChannels");
      Dictionary<int, BaseSubChannel>.Enumerator en = _mapSubChannels.GetEnumerator();
      while (en.MoveNext())
      {
        en.Current.Value.Decompose();
      }
      _mapSubChannels.Clear();
      _subChannelId = 0;
    }

    /// <summary>
    /// Gets the sub channel.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns></returns>
    public ITvSubChannel GetSubChannel(int id)
    {
      if (_mapSubChannels != null && _mapSubChannels.ContainsKey(id))
      {
        return _mapSubChannels[id];
      }
      return null;
    }

    /// <summary>
    /// Gets the sub channels.
    /// </summary>
    /// <value>The sub channels.</value>
    public ITvSubChannel[] SubChannels
    {
      get
      {
        int count = 0;
        ITvSubChannel[] channels = new ITvSubChannel[_mapSubChannels.Count];
        Dictionary<int, BaseSubChannel>.Enumerator en = _mapSubChannels.GetEnumerator();
        while (en.MoveNext())
        {
          channels[count++] = en.Current.Value;
        }
        return channels;
      }
    }

    #endregion
  }
}