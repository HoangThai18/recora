# FFmpeg components Recora ships. Sourced by build.sh and verify.sh, so the list lives in one place.
# Adding a component changes the CI cache key (hash of tools/ffmpeg/**) and triggers a rebuild.

FFMPEG_VERSION=8.1.3
FFMPEG_SHA256=7138d28c96d9d3e3af4ee3d8cad72741f8ffb40da90c1112235dea3ecd3178a3

# Capture sources: lavfi wraps ddagrab/gfxcapture so the video input opens before the audio pipe.
INDEVS="lavfi gdigrab dshow"

FILTERS="
  ddagrab gfxcapture scale_d3d11 hwmap hwdownload hwupload vpp_qsv
  format scale fps null crop pad setpts split
  anull aresample aformat volume amix asetpts asplit
  nullsrc color testsrc2 anullsrc sine
"

ENCODERS="
  h264_nvenc hevc_nvenc av1_nvenc
  h264_amf hevc_amf av1_amf
  h264_qsv hevc_qsv av1_qsv
  h264_mf hevc_mf aac_mf mp3_mf
  libx264 aac pcm_s16le pcm_f32le rawvideo wrapped_avframe
"

# wrapped_avframe: the lavfi device hands GPU frames over as wrapped AVFrames.
# mjpeg/h264/rawvideo: what webcams and capture cards send through dshow.
DECODERS="wrapped_avframe rawvideo mjpeg h264 hevc pcm_f32le pcm_s16le aac mp3float"

DEMUXERS="concat matroska mov pcm_f32le pcm_s16le wav aac mp3 rawvideo"
MUXERS="matroska mp4 mov ipod mp3 wav adts null rawvideo pcm_f32le"
PARSERS="h264 hevc av1 aac mpegaudio mjpeg"
PROTOCOLS="file pipe"

# Must exist in the built binary, or the app loses a capture path (checked by verify.sh).
REQUIRED_FILTERS="ddagrab scale_d3d11 hwmap hwdownload vpp_qsv"
REQUIRED_ENCODERS="h264_nvenc hevc_nvenc av1_nvenc h264_amf hevc_amf av1_amf h264_qsv hevc_qsv av1_qsv h264_mf hevc_mf libx264 aac mp3_mf"
MAX_FFMPEG_BYTES=$((20 * 1024 * 1024))
