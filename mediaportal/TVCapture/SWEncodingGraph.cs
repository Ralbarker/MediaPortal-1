using System;
using Microsoft.Win32;
using System.Drawing;
using System.Runtime.InteropServices;
using DShowNET;
using MediaPortal.Util;
using MediaPortal.GUI.Library;

namespace MediaPortal.TV.Recording
{
	/// <summary>
	/// 
	/// </summary>
	public class SWEncodingGraph : MediaPortal.TV.Recording.IGraph
	{
    enum State
    { 
      None, 
      Created, 
      Recording, 
      Viewing
    };

    int m_iCurrentChannel = 28;
    int m_iCountryCode = 31;
    bool m_bUseCable = false;
    State m_graphState = State.None;
    string m_strVideoCaptureFilter = "";
    string m_strAudioCaptureFilter = "";
    string m_strVideoCompressor = "";
    string m_strAudioCompressor = "";
    string m_strAudioInputPin = "";
    IGraphBuilder m_graphBuilder = null;
    ICaptureGraphBuilder2 m_captureGraphBuilder = null;
    IBaseFilter m_filterCaptureVideo = null;
    IBaseFilter m_filterCaptureAudio = null;
    
    IFileSinkFilter	        m_fileWriterFilter = null; // DShow Filter: file writer
    IBaseFilter		          m_muxFilter = null; // DShow Filter: multiplexor (combine video and audio streams)
    IBaseFilter             m_filterCompressorVideo = null;
    IBaseFilter             m_filterCompressorAudio = null;
    IAMTVTuner              m_TVTuner = null;
    IAMAnalogVideoDecoder   m_IAMAnalogVideoDecoder=null;
    int				              m_rotCookie = 0; // Cookie into the Running Object Table
    VideoCaptureDevice      m_videoCaptureDevice = null;
    IVideoWindow            m_videoWindow = null;
    IBasicVideo2            m_basicVideo = null;
    IMediaControl					  m_mediaControl = null;
    Size                    m_FrameSize;
    double                  m_FrameRate;
    int                     _RecordingLevel=100;
    bool                    m_bFirstTune = true;
    int                     m_iPrevChannel=-1;

    const int WS_CHILD = 0x40000000;
    const int WS_CLIPCHILDREN = 0x02000000;
    const int WS_CLIPSIBLINGS = 0x04000000;

		public SWEncodingGraph(int iCountryCode, bool bCable, string strVideoCaptureFilter, string strAudioCaptureFilter, string strVideoCompressor, string strAudioCompressor, Size frameSize, double frameRate, string strAudioInputPin, int RecordingLevel)
    {
      m_bFirstTune = true;
      m_bUseCable = bCable;
      m_iCountryCode = iCountryCode;
      m_graphState = State.None;
      m_strVideoCaptureFilter = strVideoCaptureFilter;
      m_strAudioCaptureFilter = strAudioCaptureFilter;
      m_strVideoCompressor = strVideoCompressor;
      m_strAudioCompressor = strAudioCompressor;
      m_FrameSize = frameSize;
      m_FrameRate = frameRate;
      if (strAudioInputPin != null && strAudioInputPin.Length > 0)
        m_strAudioInputPin = strAudioInputPin;
      _RecordingLevel = RecordingLevel;
		}

    /// <summary>
    /// Creates a new DirectShow graph for the TV capturecard
    /// </summary>
    /// <returns>bool indicating if graph is created or not</returns>
    public bool CreateGraph()
    {
      if (m_graphState != State.None) return false;
      DirectShowUtil.DebugWrite("SWGraph:CreateGraph()");

      // find the video capture device
      m_iPrevChannel=-1;
      m_bFirstTune = true;
      Filters filters = new Filters();
      Filter filterVideoCaptureDevice = null;
      Filter filterAudioCaptureDevice = null;
      foreach (Filter filter in filters.VideoInputDevices)
      {
        if (filter.Name.Equals(m_strVideoCaptureFilter))
        {
          filterVideoCaptureDevice = filter;
          break;
        }
      }
      // find the audio capture device
      if (m_strAudioCaptureFilter.Length > 0)
      {
        foreach (Filter filter in filters.AudioInputDevices)
        {
          if (filter.Name.Equals(m_strAudioCaptureFilter))
          {
            filterAudioCaptureDevice = filter;
            break;
          }
        }
      }

      if (filterVideoCaptureDevice == null) 
      {
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() FAILED couldnt find capture device:{0}",m_strVideoCaptureFilter);
        return false;
      }
      if (filterAudioCaptureDevice == null && m_strAudioCaptureFilter.Length > 0) 
      {
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() FAILED couldnt find capture device:{0}",m_strAudioCaptureFilter);
        return false;
      }

      // Make a new filter graph
      DirectShowUtil.DebugWrite("SWGraph:create new filter graph (IGraphBuilder)");
      m_graphBuilder = (IGraphBuilder) Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph, true));

      // Get the Capture Graph Builder
      DirectShowUtil.DebugWrite("SWGraph:Get the Capture Graph Builder (ICaptureGraphBuilder2)");
      Guid clsid = Clsid.CaptureGraphBuilder2;
      Guid riid = typeof(ICaptureGraphBuilder2).GUID;
      m_captureGraphBuilder = (ICaptureGraphBuilder2) DsBugWO.CreateDsInstance(ref clsid, ref riid);

      DirectShowUtil.DebugWrite("SWGraph:Link the CaptureGraphBuilder to the filter graph (SetFiltergraph)");
      int hr = m_captureGraphBuilder.SetFiltergraph(m_graphBuilder);
      if (hr < 0) 
      {
        DirectShowUtil.DebugWrite("SWGraph:link FAILED:0x{0:X}",hr);
        return false;
      }
      DirectShowUtil.DebugWrite("SWGraph:Add graph to ROT table");
      DsROT.AddGraphToRot(m_graphBuilder, out m_rotCookie);

      // Get the video device and add it to the filter graph
      DirectShowUtil.DebugWrite("SWGraph:CreateGraph() add capture device {0}",m_strVideoCaptureFilter);
      m_filterCaptureVideo = Marshal.BindToMoniker(filterVideoCaptureDevice.MonikerString) as IBaseFilter;
      if (m_filterCaptureVideo != null)
      {
        hr = m_graphBuilder.AddFilter(m_filterCaptureVideo, filterVideoCaptureDevice.Name);
        if (hr < 0) 
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:Add Videodevice to filtergraph:0x{0:X}",hr);
          return false;
        }
      }

      // Get the audio device and add it to the filter graph
      if (filterAudioCaptureDevice != null)
      {
        // Get the audio device and add it to the filter graph
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() add capture device {0}",m_strAudioCaptureFilter);
        m_filterCaptureAudio = Marshal.BindToMoniker(filterAudioCaptureDevice.MonikerString) as IBaseFilter;
        if (m_filterCaptureAudio != null)
        {
          hr = m_graphBuilder.AddFilter(m_filterCaptureAudio, filterAudioCaptureDevice.Name);
          if (hr < 0) 
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED:Add audiodevice to filtergraph:0x{0:X}",hr);
            return false;
          }
        }
      }

      // Retrieve TV Tuner if available
      DirectShowUtil.DebugWrite("SWGraph:Find TV Tuner");
      object o = null;
      Guid cat = FindDirection.UpstreamOnly;
      Guid iid = typeof(IAMTVTuner).GUID;
      hr = m_captureGraphBuilder.FindInterface(new Guid[1] { cat}, null, m_filterCaptureVideo, ref iid, out o);
      if (hr == 0) 
      {
        m_TVTuner = o as IAMTVTuner;
      }
      if (m_TVTuner == null)
      {
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() FAILED:no tuner found");
      }

      m_videoCaptureDevice = new VideoCaptureDevice(m_graphBuilder, m_captureGraphBuilder, m_filterCaptureVideo);



      //set the frame size
      m_videoCaptureDevice.SetFrameSize(m_FrameSize);
      m_videoCaptureDevice.SetFrameRate(m_FrameRate);

      m_IAMAnalogVideoDecoder = m_filterCaptureVideo as IAMAnalogVideoDecoder;

      m_graphState = State.Created;
      return true;
    }

    /// <summary>
    /// Deletes the current DirectShow graph created with CreateGraph()
    /// </summary>
    /// <remarks>
    /// Graph must be created first with CreateGraph()
    /// </remarks>
    public void DeleteGraph()
    {
      if (m_graphState < State.Created) return;
      m_iPrevChannel=-1;

      DirectShowUtil.DebugWrite("SWGraph:DeleteGraph()");
      StopRecording();
      StopViewing();

      if (m_mediaControl != null)
      {
        m_mediaControl.Stop();
      }
      if (m_videoWindow != null)
      {
        m_videoWindow.put_Visible(DsHlp.OAFALSE);
        m_videoWindow.put_Owner(IntPtr.Zero);
        m_videoWindow = null;
      }

      if (m_videoCaptureDevice != null)
      {
        m_videoCaptureDevice.CloseInterfaces();
        m_videoCaptureDevice = null;
      }
      if (m_IAMAnalogVideoDecoder!=null)
        Marshal.ReleaseComObject( m_IAMAnalogVideoDecoder ); m_IAMAnalogVideoDecoder = null;

      if (m_filterCaptureAudio != null)
        Marshal.ReleaseComObject(m_filterCaptureAudio); m_filterCaptureAudio = null;

      if (m_filterCompressorVideo != null)
        Marshal.ReleaseComObject(m_filterCompressorVideo); m_filterCompressorVideo = null;

      if (m_filterCompressorAudio != null)
        Marshal.ReleaseComObject(m_filterCompressorAudio); m_filterCompressorAudio = null;

      if (m_muxFilter != null)
        Marshal.ReleaseComObject(m_muxFilter); m_muxFilter = null;
   
      if (m_fileWriterFilter != null)
        Marshal.ReleaseComObject(m_fileWriterFilter); m_fileWriterFilter = null;

      if (m_TVTuner != null)
        Marshal.ReleaseComObject(m_TVTuner); m_TVTuner = null;

      m_basicVideo = null;
      m_mediaControl = null;
      
      if (m_filterCaptureVideo != null)
        Marshal.ReleaseComObject(m_filterCaptureVideo); m_filterCaptureVideo = null;

      DsUtils.RemoveFilters(m_graphBuilder);

      if (m_rotCookie != 0)
        DsROT.RemoveGraphFromRot(ref m_rotCookie);
      m_rotCookie = 0;



      if (m_captureGraphBuilder != null)
        Marshal.ReleaseComObject(m_captureGraphBuilder); m_captureGraphBuilder = null;

	
      if (m_graphBuilder != null)
        Marshal.ReleaseComObject(m_graphBuilder); m_graphBuilder = null;

      m_graphState = State.None;
      return;
    }

    /// <summary>
    /// Starts timeshifting the TV channel and stores the timeshifting 
    /// files in the specified filename
    /// </summary>
    /// <param name="iChannelNr">TV channel to which card should be tuned</param>
    /// <param name="strFileName">Filename for the timeshifting buffers</param>
    /// <returns>boolean indicating if timeshifting is running or not</returns>
    /// <remarks>
    /// Graph must be created first with CreateGraph()
    /// </remarks>
    public bool StartTimeShifting(AnalogVideoStandard standard,int iChannelNr, string strFileName)
    {
      return true;
    }
    
    /// <summary>
    /// Stops timeshifting and cleans up the timeshifting files
    /// </summary>
    /// <returns>boolean indicating if timeshifting is stopped or not</returns>
    /// <remarks>
    /// Graph should be timeshifting 
    /// </remarks>
    public bool StopTimeShifting()
    {
      return true;
    }


    /// <summary>
    /// Starts recording live TV to a file
    /// <param name="strFileName">filename for the new recording</param>
    /// <param name="bContentRecording">Specifies whether a content or reference recording should be made</param>
    /// <param name="timeProgStart">Contains the starttime of the current tv program</param>
    /// </summary>
    /// <returns>boolean indicating if recorded is started or not</returns> 
    /// <remarks>
    /// Graph should be timeshifting. When Recording is started the graph is still 
    /// timeshifting
    /// 
    /// A content recording will start recording from the moment this method is called
    /// and ignores any data left/present in the timeshifting buffer files
    /// 
    /// A reference recording will start recording from the moment this method is called
    /// It will examine the timeshifting files and try to record as much data as is available
    /// from the timeProgStart till the moment recording is stopped again
    /// </remarks>
    public bool StartRecording(AnalogVideoStandard standard,int iChannelNr, ref string strFileName, bool bContentRecording, DateTime timeProgStart)
    {
      if (m_graphState == State.Recording) return true;
      if (m_graphState != State.Created) return false;
      

      SetRegistryThings();
      int hr;
      DirectShowUtil.DebugWrite("SWGraph:Start recording...");
      Filters filters = new Filters();
      Filter filterVideoCompressor = null;
      Filter filterAudioCompressor = null;

      bool bRecordWMV = false;
      strFileName = System.IO.Path.ChangeExtension(strFileName, ".avi");
      
      DirectShowUtil.DebugWrite("SWGraph:find video compressor filter...");
      foreach (Filter filter in filters.VideoCompressors)
      {
        if (filter.Name.Equals(m_strVideoCompressor))
        {
          filterVideoCompressor = filter;
          // check for wmv 7,8,9 DMP
          if (filter.MonikerString.Equals(@"@device:dmo:{3181343B-94A2-4FEB-ADEF-30A1DDE617B4}{33D9A760-90C8-11D0-BD43-00A0C911CE86}") || 
              filter.MonikerString.Equals(@"@device:dmo:{BA2F0162-CAA4-48FC-89EA-DB0D1DFF40CA}{33D9A760-90C8-11D0-BD43-00A0C911CE86}") || 
              filter.MonikerString.Equals(@"@device:dmo:{96B57CDD-8966-410C-BB1F-C97EEA765C04}{33D9A760-90C8-11D0-BD43-00A0C911CE86}") ||
            
              filter.MonikerString.Equals(@"@device:cm:{33D9A760-90C8-11D0-BD43-00A0C911CE86}\wmv3")  )
          {
            bRecordWMV = true;
            strFileName = System.IO.Path.ChangeExtension(strFileName, ".wmv");
          }
          break;
        }
      }
      
      DirectShowUtil.DebugWrite("SWGraph:find audio compressor filter...");
      foreach (Filter filter in filters.AudioCompressors)
      {
        if (filter.Name.Equals(m_strAudioCompressor))
        {
          filterAudioCompressor = filter;
          break;
        }
      }

      if (filterVideoCompressor == null) 
      {
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() FAILED couldnt find video compressor:{0}",m_strVideoCompressor);
        return false;
      }

      if (filterAudioCompressor == null) 
      {
        DirectShowUtil.DebugWrite("SWGraph:CreateGraph() FAILED couldnt find audio compressor:{0}",m_strAudioCompressor);
        return false;
      }

      // add the video/audio compressor filters
      DirectShowUtil.DebugWrite("SWGraph:CreateGraph() add video compressor {0}",m_strVideoCompressor);
      m_filterCompressorVideo = Marshal.BindToMoniker(filterVideoCompressor.MonikerString) as IBaseFilter;
      if (m_filterCompressorVideo != null)
      {
        hr = m_graphBuilder.AddFilter(m_filterCompressorVideo, filterVideoCompressor.Name);
        if (hr < 0) 
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:Add video compressor to filtergraph:0x{0:X}",hr);
          return false;
        }
      }
      else
      {
        DirectShowUtil.DebugWrite("SWGraph:FAILED:Add video compressor to filtergraph");
        return false;
      }

      DirectShowUtil.DebugWrite("SWGraph:CreateGraph() add audio compressor {0}",m_strAudioCompressor);
      m_filterCompressorAudio = Marshal.BindToMoniker(filterAudioCompressor.MonikerString) as IBaseFilter;
      if (m_filterCompressorAudio != null)
      {
        hr = m_graphBuilder.AddFilter(m_filterCompressorAudio, filterAudioCompressor.Name);
        if (hr < 0) 
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:Add audio compressor to filtergraph");
          return false;
        }
      }
      else
      {
        DirectShowUtil.DebugWrite("SWGraph:FAILED:Add audio compressor to filtergraph:0x{0:X}",hr);
        return false;
      }

      // select the correct audio input pin to capture
      if (m_filterCaptureAudio != null)
      {
        if (m_strAudioInputPin.Length > 0)
        {
          DirectShowUtil.DebugWrite("SWGraph:set audio input pin:{0}", m_strAudioInputPin);
          IPin pinInput = DirectShowUtil.FindPin(m_filterCaptureAudio, PinDirection.Input, m_strAudioInputPin);
          if (pinInput == null)
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED audio input pin:{0} not found", m_strAudioInputPin);
          }
          else
          {
            IAMAudioInputMixer mixer = pinInput as IAMAudioInputMixer;
            if (mixer != null)
            {
              hr = mixer.put_Enable(true);
              if (hr != 0)
              {
                DirectShowUtil.DebugWrite("SWGraph:FAILED:to enable audio input pin:0x{0:X}",hr);
              }
              else
              {
                DirectShowUtil.DebugWrite("SWGraph:enabled audio input pin:{0}",m_strAudioInputPin);
              }

              double fLevel=((double)_RecordingLevel);
              fLevel /= 100.0d;
              hr = mixer.put_MixLevel(fLevel);
              if (hr != 0)
              {
                DirectShowUtil.DebugWrite("SWGraph:FAILED:to set mixing level to {0}%:0x{1:X}",_RecordingLevel,hr);
              }
              else
              {
                DirectShowUtil.DebugWrite("SWGraph:set mixing level to {0}% of pin:{1}",_RecordingLevel,m_strAudioInputPin);
              }

            }
            else
            {
              DirectShowUtil.DebugWrite("SWGraph:FAILED audio input pin:{0} does not expose an IAMAudioInputMixer", m_strAudioInputPin);
            }
          }
        }
      }
    
      // set filename
      DirectShowUtil.DebugWrite("SWGraph:record to :{0} ", strFileName);

      Guid cat, med;
      Guid mediaSubTypeAvi = MediaSubType.Avi;
      if (bRecordWMV)
        mediaSubTypeAvi = MediaSubType.Asf;

      hr = m_captureGraphBuilder.SetOutputFileName(ref mediaSubTypeAvi, strFileName, out m_muxFilter, out m_fileWriterFilter);
      if (hr != 0)
      {
        DirectShowUtil.DebugWrite("SWGraph:FAILED:to set output filename to :{0} :0x{1:X}", strFileName, hr);
        return false;
      }

      if (bRecordWMV)
      {
        DirectShowUtil.DebugWrite("SWGraph:get IConfigAsfWriter");
        IConfigAsfWriter asfwriter = m_fileWriterFilter as IConfigAsfWriter;
        if (asfwriter != null)
        {
          DirectShowUtil.DebugWrite("SWGraph:IConfigAsfWriter.SetProfile(BestVBRVideo)");
          //Guid WMProfile_V80_HIGHVBRVideo = new Guid( 0xf10d9d3,0x3b04,0x4fb0,0xa3, 0xd3, 0x88, 0xd4, 0xac, 0x85, 0x4a, 0xcc);
          Guid WMProfile_V80_BESTVBRVideo = new Guid(0x48439ba, 0x309c, 0x440e, 0x9c, 0xb4, 0x3d, 0xcc, 0xa3, 0x75, 0x64, 0x23);
          hr = asfwriter.ConfigureFilterUsingProfileGuid(WMProfile_V80_BESTVBRVideo);
          if (hr != 0)
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED IConfigAsfWriter.SetProfile() :0x{0:X}", hr);
          }
        }
        else DirectShowUtil.DebugWrite("SWGraph:FAILED:to get IConfigAsfWriter");
      }

      if (m_videoCaptureDevice.CapturePin != null)
      {
        // NOTE that we try to render the interleaved pin before the video pin, because
        // if BOTH exist, it's a DV filter and the only way to get the audio is to use
        // the interleaved pin.  Using the Video pin on a DV filter is only useful if
        // you don't want the audio.
        DirectShowUtil.DebugWrite("SWGraph:videocap:connect video capture->compressor (interleaved)");
        cat = PinCategory.Capture;
        med = MediaType.Interleaved;
        hr = m_captureGraphBuilder.RenderStream(new Guid[1] { cat}, new Guid[1] { med}, m_filterCaptureVideo, m_filterCompressorVideo, m_muxFilter);
        if (hr != 0)
        {
          DirectShowUtil.DebugWrite("SWGraph:videocap:connect video capture->compressor (video)");
          cat = PinCategory.Capture;
          med = MediaType.Video;
          hr = m_captureGraphBuilder.RenderStream(new Guid[1] { cat}, new Guid[1] { med}, m_filterCaptureVideo, m_filterCompressorVideo, m_muxFilter);
          if (hr != 0)
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED:videocap:to connect video capture->compressor :0x{0:X}",hr);
            return false;
          }
        }

        if (m_filterCaptureAudio == null)
        {
          DirectShowUtil.DebugWrite("SWGraph:videocap:connect audio capture->compressor ");
          cat = PinCategory.Capture;
          med = MediaType.Audio;
          hr = m_captureGraphBuilder.RenderStream(new Guid[1] { cat}, new Guid[1] { med}, m_filterCaptureVideo, m_filterCompressorAudio, m_muxFilter);
          if (hr == 0)
          {
            DirectShowUtil.DebugWrite("SWGraph:videocap:connect audio capture->compressor :succeeded");
          }
        }
      }


      if (m_filterCaptureAudio != null)
      {
        DirectShowUtil.DebugWrite("SWGraph:audiocap:connect audio capture->compressor ");
        cat = PinCategory.Capture;
        med = MediaType.Audio;
        hr = m_captureGraphBuilder.RenderStream(new Guid[1] { cat}, new Guid[1] { med}, m_filterCaptureAudio, m_filterCompressorAudio, m_muxFilter);
        if (hr != 0)
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:audiocap:to connect audio capture->compressor :0x{0:X}",hr);
          return false;
        }
      } 

      // Set the audio as the masterstream
      if (!bRecordWMV)
      {
        // set avi muxing parameters
        IConfigAviMux ConfigMux = m_muxFilter as IConfigAviMux;
        if (ConfigMux != null)
        {
          DirectShowUtil.DebugWrite("SWGraph:set audio as masterstream");
          hr = ConfigMux.SetMasterStream(1);
          if (hr != 0)
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED:to set audio as masterstream:0x{0:X}",hr);
          }
        }
        else
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:to get IConfigAviMux");
        }

        // Set the avi interleaving mode
        IConfigInterleaving InterleaveMode = m_muxFilter as IConfigInterleaving;
        if (InterleaveMode != null)
        {
          DirectShowUtil.DebugWrite("SWGraph:set avi interleave mode");
          hr = InterleaveMode.put_Mode(AviInterleaveMode.INTERLEAVE_CAPTURE);
          if (hr != 0)
          {
            DirectShowUtil.DebugWrite("SWGraph:FAILED:to set avi interleave mode:0x{0:X}",hr);
          }
        }
        else
        {
          DirectShowUtil.DebugWrite("SWGraph:FAILED:to get IConfigInterleaving");
        }
      }//if (!bRecordWMV)

      if (m_mediaControl == null)
        m_mediaControl = (IMediaControl)m_graphBuilder;

      TuneChannel(standard, iChannelNr);

      m_mediaControl.Run();
      m_graphState = State.Recording;

      DirectShowUtil.DebugWrite("SWGraph:recording...");
      return true;
    }
    
    
    /// <summary>
    /// Stops recording 
    /// </summary>
    /// <remarks>
    /// Graph should be recording. When Recording is stopped the graph is still 
    /// timeshifting
    /// </remarks>
    public void StopRecording()
    {
      if (m_graphState != State.Recording) return;
      DirectShowUtil.DebugWrite("SWGraph:stop recording...");
      m_mediaControl.Stop();
      m_graphState = State.Created;
      DeleteGraph();
      DirectShowUtil.DebugWrite("SWGraph:stopped recording...");
    }



    /// <summary>
    /// Switches / tunes to another TV channel
    /// </summary>
    /// <param name="iChannel">New channel</param>
    /// <remarks>
    /// Graph should be timeshifting. 
    /// </remarks>
    public void TuneChannel(AnalogVideoStandard standard,int iChannel)
    {
      m_iCurrentChannel = iChannel;

      DirectShowUtil.DebugWrite("SWGraph:TuneChannel() tune to channel:{0}", iChannel);
      if (iChannel < 1000)
      {
        if (m_TVTuner == null) return;
        if (m_bFirstTune)
        {
          m_bFirstTune = false;
          m_TVTuner.put_TuningSpace(0);
          m_TVTuner.put_CountryCode(m_iCountryCode);
          m_TVTuner.put_Mode(DShowNET.AMTunerModeType.TV);
          if (m_bUseCable)
            m_TVTuner.put_InputType(0, DShowNET.TunerInputType.Cable);
          else
            m_TVTuner.put_InputType(0, DShowNET.TunerInputType.Antenna);
          if (m_IAMAnalogVideoDecoder!=null)
          {
            DirectShowUtil.DebugWrite("SWGraph:Select tvformat:{0}", standard.ToString());
            int hr=m_IAMAnalogVideoDecoder.put_TVFormat(standard);
            if (hr!=0) DirectShowUtil.DebugWrite("SWGraph:Unable to select tvformat:{0}", standard.ToString());
          }
          m_TVTuner.get_TVFormat(out standard);
        }
        try
        {

          if (m_IAMAnalogVideoDecoder!=null)
          {
            DirectShowUtil.DebugWrite("SWGraph:Select tvformat:{0}", standard.ToString());
            int hr=m_IAMAnalogVideoDecoder.put_TVFormat(standard);
            if (hr!=0) DirectShowUtil.DebugWrite("SWGraph:Unable to select tvformat:{0}", standard.ToString());
          }
          m_TVTuner.get_TVFormat(out standard);

          m_TVTuner.put_Channel(iChannel, DShowNET.AMTunerSubChannel.Default, DShowNET.AMTunerSubChannel.Default);

          int iFreq;
          double dFreq;
          m_TVTuner.get_VideoFrequency(out iFreq);
          dFreq = iFreq / 1000000d;
          DirectShowUtil.DebugWrite("SWGraph:TuneChannel() tuned to {0} MHz. tvformat:{1}", dFreq,standard.ToString());
        }
        catch (Exception) {} 
      }
      else
      {
        if (m_IAMAnalogVideoDecoder!=null)
        {
          if (standard != AnalogVideoStandard.None)
          {
            DirectShowUtil.DebugWrite("SWGraph:Select tvformat:{0}", standard.ToString());
            int hr=m_IAMAnalogVideoDecoder.put_TVFormat(standard);
            if (hr!=0) DirectShowUtil.DebugWrite("SWGraph:Unable to select tvformat:{0}", standard.ToString());
          }
        }
      }
      DirectShowUtil.DebugWrite("SWGraph:TuneChannel() tuningspace:0 country:{0} tv standard:{1} cable:{2}",
                                  m_iCountryCode,standard.ToString(),
                                  m_bUseCable);

      bool bFixCrossbar=true;
      if (m_iPrevChannel>=0)
      {
        if (m_iPrevChannel< 1000 && iChannel < 1000) bFixCrossbar=false;
        if (m_iPrevChannel==1000 && iChannel ==1000) bFixCrossbar=false;
        if (m_iPrevChannel==1001 && iChannel ==1001) bFixCrossbar=false;
        if (m_iPrevChannel==1002 && iChannel ==1002) bFixCrossbar=false;
      }
      if (bFixCrossbar)
      {
        DsUtils.FixCrossbarRouting(m_captureGraphBuilder,m_filterCaptureVideo, iChannel<1000, (iChannel==1001), (iChannel==1002), (iChannel==1000) );
      }
      m_iPrevChannel=iChannel;
    }

    /// <summary>
    /// Returns the current tv channel
    /// </summary>
    /// <returns>Current channel</returns>
    public int GetChannelNumber()
    {
      return m_iCurrentChannel;
    }

    /// <summary>
    /// Property indiciating if the graph supports timeshifting
    /// </summary>
    /// <returns>boolean indiciating if the graph supports timeshifting</returns>
    public bool SupportsTimeshifting()
    {
      return false;
    }


    /// <summary>
    /// Starts viewing the TV channel 
    /// </summary>
    /// <param name="iChannelNr">TV channel to which card should be tuned</param>
    /// <returns>boolean indicating if succeed</returns>
    /// <remarks>
    /// Graph must be created first with CreateGraph()
    /// </remarks>
    public bool StartViewing(AnalogVideoStandard standard, int iChannelNr)
    {
      ///@@@todo
      if (m_graphState != State.Created) return false;
      TuneChannel(standard, iChannelNr);

      m_videoCaptureDevice.RenderPreview();

      m_videoWindow = (IVideoWindow) m_graphBuilder as IVideoWindow;
      if (m_videoWindow==null)
      {
        Log.Write("SWGraph:FAILED:Unable to get IVideoWindow");
        return false;
      }

      m_basicVideo = m_graphBuilder as IBasicVideo2;
      if (m_basicVideo==null)
      {
        Log.Write("SWGraph:FAILED:Unable to get IBasicVideo2");
        return false;
      }

      m_mediaControl = (IMediaControl)m_graphBuilder;
      int hr = m_videoWindow.put_Owner(GUIGraphicsContext.form.Handle);
      if (hr != 0) 
        DirectShowUtil.DebugWrite("SWGraph:FAILED:set Video window:0x{0:X}",hr);

      hr = m_videoWindow.put_WindowStyle(WS_CHILD | WS_CLIPCHILDREN | WS_CLIPSIBLINGS);
      if (hr != 0) 
        DirectShowUtil.DebugWrite("SWGraph:FAILED:set Video window style:0x{0:X}",hr);

      hr = m_videoWindow.put_Visible(DsHlp.OATRUE);
      if (hr != 0) 
        DirectShowUtil.DebugWrite("SWGraph:FAILED:put_Visible:0x{0:X}",hr);

      DirectShowUtil.DebugWrite("SWGraph:enable deinterlace");
      DirectShowUtil.EnableDeInterlace(m_graphBuilder);

      m_mediaControl.Run();
      
      GUIGraphicsContext.OnVideoWindowChanged += new VideoWindowChangedHandler(GUIGraphicsContext_OnVideoWindowChanged);
      m_graphState = State.Viewing;
      GUIGraphicsContext_OnVideoWindowChanged();
      return true;
    }


    /// <summary>
    /// Stops viewing the TV channel 
    /// </summary>
    /// <returns>boolean indicating if succeed</returns>
    /// <remarks>
    /// Graph must be viewing first with StartViewing()
    /// </remarks>
    public bool StopViewing()
    {
      if (m_graphState != State.Viewing) return false;
       
      GUIGraphicsContext.OnVideoWindowChanged -= new VideoWindowChangedHandler(GUIGraphicsContext_OnVideoWindowChanged);
      DirectShowUtil.DebugWrite("SWGraph:StopViewing()");
      m_videoWindow.put_Visible(DsHlp.OAFALSE);
      m_mediaControl.Stop();
      m_graphState = State.Created;
      DeleteGraph();
      return true;
    }

    /// <summary>
    /// Callback from GUIGraphicsContext. Will get called when the video window position or width/height changes
    /// </summary>
    private void GUIGraphicsContext_OnVideoWindowChanged()
    {
      if (m_graphState != State.Viewing) return;
      int iVideoWidth, iVideoHeight;
      m_basicVideo.GetVideoSize(out iVideoWidth, out iVideoHeight);
      
      if (GUIGraphicsContext.IsFullScreenVideo)
      {
        float x = GUIGraphicsContext.OverScanLeft;
        float y = GUIGraphicsContext.OverScanTop;
        int nw = GUIGraphicsContext.OverScanWidth;
        int nh = GUIGraphicsContext.OverScanHeight;
        if (nw <= 0 || nh <= 0) return;


        System.Drawing.Rectangle rSource, rDest;
        MediaPortal.GUI.Library.Geometry m_geometry = new MediaPortal.GUI.Library.Geometry();
        m_geometry.ImageWidth = iVideoWidth;
        m_geometry.ImageHeight = iVideoHeight;
        m_geometry.ScreenWidth = nw;
        m_geometry.ScreenHeight = nh;
        m_geometry.ARType = GUIGraphicsContext.ARType;
        m_geometry.PixelRatio = GUIGraphicsContext.PixelRatio;
        m_geometry.GetWindow(out rSource, out rDest);
        rDest.X += (int)x;
        rDest.Y += (int)y;

        m_basicVideo.SetSourcePosition(rSource.Left, rSource.Top, rSource.Width, rSource.Height);
        m_basicVideo.SetDestinationPosition(0, 0, rDest.Width, rDest.Height);
        m_videoWindow.SetWindowPosition(rDest.Left, rDest.Top, rDest.Width, rDest.Height);
        DirectShowUtil.DebugWrite("SWGraph: capture size:{0}x{1}",iVideoWidth, iVideoHeight);
        DirectShowUtil.DebugWrite("SWGraph: source position:({0},{1})-({2},{3})",rSource.Left, rSource.Top, rSource.Right, rSource.Bottom);
        DirectShowUtil.DebugWrite("SWGraph: dest   position:({0},{1})-({2},{3})",rDest.Left, rDest.Top, rDest.Right, rDest.Bottom);
      }
      else
      {
        m_basicVideo.SetSourcePosition(0, 0, iVideoWidth, iVideoHeight);
        m_basicVideo.SetDestinationPosition(0, 0, GUIGraphicsContext.VideoWindow.Width, GUIGraphicsContext.VideoWindow.Height);
        m_videoWindow.SetWindowPosition(GUIGraphicsContext.VideoWindow.Left, GUIGraphicsContext.VideoWindow.Top, GUIGraphicsContext.VideoWindow.Width, GUIGraphicsContext.VideoWindow.Height);
        DirectShowUtil.DebugWrite("SWGraph: capture size:{0}x{1}",iVideoWidth, iVideoHeight);
        DirectShowUtil.DebugWrite("SWGraph: source position:({0},{1})-({2},{3})",0, 0, iVideoWidth, iVideoHeight);
        DirectShowUtil.DebugWrite("SWGraph: dest   position:({0},{1})-({2},{3})",GUIGraphicsContext.VideoWindow.Left, GUIGraphicsContext.VideoWindow.Top, GUIGraphicsContext.VideoWindow.Right, GUIGraphicsContext.VideoWindow.Bottom);

      }

    }

    void SetRegistryThings()
    {
      //disable xvid status window while encoding
      try
      {
        RegistryKey hkcu = Registry.CurrentUser;
        RegistryKey subkey = hkcu.OpenSubKey(@"Software\GNU\XviD");
        if (subkey != null)
        {
          long uivalue=0;
          subkey.SetValue("display_status", (int)uivalue);
        }
      }
      catch(Exception)
      {
      }
    }

    
    /// <summary>
    /// This method can be used to ask the graph if it should be rebuild when
    /// we want to tune to the new channel:ichannel
    /// </summary>
    /// <param name="iChannel">new channel to tune to</param>
    /// <returns>true : graph needs to be rebuild for this channel
    ///          false: graph does not need to be rebuild for this channel
    /// </returns>
    public bool ShouldRebuildGraph(int iChannel)
    {
      // if we switch from tuner <-> SVHS/Composite then 
      // we need to rebuild the capture graph
      bool bFixCrossbar=true;
      if (m_iPrevChannel>=0)
      {
        // tuner : channel < 1000
        // SVHS/composite : channel >=1000
        if (m_iPrevChannel< 1000 && iChannel < 1000) bFixCrossbar=false;
        if (m_iPrevChannel==1000 && iChannel ==1000) bFixCrossbar=false;
        if (m_iPrevChannel==1001 && iChannel ==1001) bFixCrossbar=false;
        if (m_iPrevChannel==1002 && iChannel ==1002) bFixCrossbar=false;
      }
      else bFixCrossbar=false;
      return bFixCrossbar;
    }
    public bool SignalPresent()
    {
      if (m_graphState!=State.Recording && m_graphState!=State.Viewing) return false;
      if (m_TVTuner==null) return true;
      AMTunerSignalStrength strength;
      m_TVTuner.SignalPresent(out strength);
      return strength==AMTunerSignalStrength.SignalPresent;
    }

    public long VideoFrequency()
    {
      
      if (m_graphState!=State.Recording && m_graphState!=State.Viewing) return 0;
      if (m_TVTuner==null) return 0;
      int lFreq;
      m_TVTuner.get_VideoFrequency(out lFreq);
      return lFreq;
    }
  }
}
