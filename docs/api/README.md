# SharpDate API listing

Generated from the XML doc comments by `tools/generate_api_docs.py` — do not edit by hand.
Everything is in the `Playdate` namespace except the raw C structs (`Playdate.Native`).
For explanations and commented examples see the [reference](../reference/README.md).

## [Game and entry point](PD.md)

- [`PD`](PD.md#pd) — The game's entry point and the raw Playdate C API (`PlaydateAPI*`).
- [`Game`](PD.md#game) — A Playdate game.

## [System: input, time, menu, device](Sys.md)

- [`Sys`](Sys.md#sys) — System services: logging, buttons, crank, accelerometer, time, the system menu, battery and device settings (C: `pd->system`; in Lua these are spread over `playdate.*`).
- [`MenuItem`](Sys.md#menuitem) — An item the game added to the system menu (C: PDMenuItem, Lua: playdate.menu.item).

## [Display, tile maps, video](Display.md)

- [`Display`](Display.md#display) — Display settings: frame rate, scale, inversion, mosaic, flipping and offset (C: `pd->display`, Lua: `playdate.display`).
- [`TileMap`](Display.md#tilemap) — A grid of tiles drawn from a `BitmapTable` (C: LCDTileMap, Lua: playdate.graphics.tilemap).
- [`VideoPlayer`](Display.md#videoplayer) — Renders frames of a .pdv video (C: LCDVideoPlayer, Lua: playdate.graphics.video).

## [Graphics, bitmaps, fonts](Graphics.md)

- [`LCDColor`](Graphics.md#lcdcolor) — A drawing color: one of the `SolidColor` values or a pointer to an 8x8 `Pattern` (C type `LCDColor`).
- [`Pattern`](Graphics.md#pattern) — An 8x8 pattern (8 rows of pixel data followed by 8 rows of mask) in native memory, usable as an `LCDColor`.
- [`Graphics`](Graphics.md#graphics) — Drawing on the screen or into a bitmap: shapes, text, bitmaps, clipping, stencils and direct frame buffer access (C: `pd->graphics`, Lua: `playdate.graphics`).
- [`Bitmap`](Graphics.md#bitmap) — An image (C: LCDBitmap, Lua: playdate.graphics.image).
- [`BitmapTable`](Graphics.md#bitmaptable) — A list of same-size images, e.g. animation frames or tiles (C: LCDBitmapTable, Lua: playdate.graphics.imagetable).
- [`Font`](Graphics.md#font) — A font (C: LCDFont, Lua: playdate.graphics.font).

## [Sprites and collisions](Sprite.md)

- [`Sprite`](Sprite.md#sprite) — A sprite: an image (or custom drawing) with a position, a Z order and an optional collision rectangle, managed by the sprite system (C: LCDSprite, Lua: playdate.graphics.sprite).
- [`Sprites`](Sprite.md#sprites) — The sprite system as a whole: drawing all sprites, queries and global settings (C: `pd->sprite`, Lua: `playdate.graphics.sprite` static functions).
- [`CollisionList`](Sprite.md#collisionlist) — The collisions returned by `Sprite.MoveWithCollisions` and `Sprite.CheckCollisions`.
- [`SpriteList`](Sprite.md#spritelist) — A list of sprites returned by a query.
- [`SpriteQueryList`](Sprite.md#spritequerylist) — The results of `Sprites.QuerySpriteInfoAlongLine`.

## [Sound](Sound.md)

- [`Sound`](Sound.md#sound) — The sound engine (C: `pd->sound`, Lua: `playdate.sound`): channels, custom sources, the microphone and headphones.
- [`SoundSource`](Sound.md#soundsource) — Anything that produces audio (C: SoundSource): `FilePlayer`, `SamplePlayer`, `Synth`, `Instrument` and delay-line taps all convert to it implicitly, so they can be added to a `SoundChannel`.
- [`FilePlayer`](Sound.md#fileplayer) — Streams a sound file from storage instead of loading it into memory: best for music (C: FilePlayer, Lua: playdate.sound.fileplayer).
- [`AudioSample`](Sound.md#audiosample) — A sound loaded into memory, for short sounds played often (C: AudioSample, Lua: playdate.sound.sample).
- [`SamplePlayer`](Sound.md#sampleplayer) — Plays an `AudioSample` (C: SamplePlayer, Lua: playdate.sound.sampleplayer).
- [`SynthSignalValue`](Sound.md#synthsignalvalue) — A value that can modulate a parameter (C: PDSynthSignalValue).
- [`SynthSignal`](Sound.md#synthsignal) — A signal computed by your own native callbacks, or one that follows another signal with a scale and offset (C: PDSynthSignal).
- [`LFO`](Sound.md#lfo) — A low-frequency oscillator to modulate pitch, volume, filters ... (C: PDSynthLFO, Lua: playdate.sound.lfo).
- [`Envelope`](Sound.md#envelope) — An attack/decay/sustain/release envelope (C: PDSynthEnvelope, Lua: playdate.sound.envelope).
- [`Synth`](Sound.md#synth) — A synthesizer voice playing a waveform, a sample, a wavetable or a custom generator (C: PDSynth, Lua: playdate.sound.synth).
- [`Instrument`](Sound.md#instrument) — A set of synth voices for polyphony (C: PDSynthInstrument, Lua: playdate.sound.instrument).
- [`ControlSignal`](Sound.md#controlsignal) — Values that change at given steps of a sequence, e.g. a MIDI controller (C: ControlSignal, Lua: playdate.sound.controlsignal).
- [`SequenceTrack`](Sound.md#sequencetrack) — One track of a `SoundSequence`: notes and control signals played on an `Instrument` (C: SequenceTrack, Lua: playdate.sound.track).
- [`SoundSequence`](Sound.md#soundsequence) — A multi-track song, usually loaded from a MIDI file (C: SoundSequence, Lua: playdate.sound.sequence).
- [`SoundChannel`](Sound.md#soundchannel) — A mixer channel with its own sources, effects, volume and pan (C: SoundChannel, Lua: playdate.sound.channel).
- [`SoundEffect`](Sound.md#soundeffect) — An effect applied to a `SoundChannel`: filters, bitcrusher, ring modulator, overdrive, delay line, or a custom native effect (C: SoundEffect, Lua: playdate.sound.effect).

## [Files](File.md)

- [`FileSystem`](File.md#filesystem) — Reading and writing files (C: `pd->file`, Lua: `playdate.file`).
- [`PDFile`](File.md#pdfile) — An open file (C: SDFile, Lua: playdate.file.file), from `FileSystem.Open`.

## [JSON](Json.md)

- [`JsonHandler`](Json.md#jsonhandler) — Receives streaming JSON callbacks while a document is decoded (C: json_decoder), so large files can be read without building a tree.
- [`JsonNode`](Json.md#jsonnode) — A node of a parsed JSON document, from `Json.Parse` or `Json.ParseFile`.
- [`Json`](Json.md#json) — Reading and writing JSON (C: `pd->json`, Lua: `playdate.datastore` / `json`).
- [`JsonWriter`](Json.md#jsonwriter) — Writes JSON text (C: json_encoder).

## [Lua interop](Lua.md)

- [`LuaFunction`](Lua.md#luafunction) — A Lua-callable function: `int f(lua_State* L)`, returning the number of values pushed.
- [`Lua`](Lua.md#lua) — Wraps `pd->lua`: register C# functions and classes with the Lua runtime and exchange values.

## [Network and scoreboards](Network.md)

- [`Scoreboards`](Network.md#scoreboards) — Online scoreboards for games distributed through Catalog (C: `pd->scoreboards`, Lua: `playdate.scoreboards`).
- [`Scoreboards.Score`](Network.md#scoreboardsscore) — A score on a board.
- [`Scoreboards.Board`](Network.md#scoreboardsboard) — A scoreboard.
- [`Network`](Network.md#network) — Wi-Fi state and control (C: `pd->network`, Lua: `playdate.network`).
- [`HttpConnection`](Network.md#httpconnection) — An HTTP or HTTPS connection to one server (C: HTTPConnection, Lua: playdate.network.http).
- [`TcpConnection`](Network.md#tcpconnection) — A TCP (optionally TLS) connection (C: TCPConnection, Lua: playdate.network.tcp).

## [Strings for native calls](Utf8.md)

- [`Utf8`](Utf8.md#utf8) — UTF-8 helpers for passing strings to the Playdate C API.

## [Enums and data structures](Types.md)

- [`SystemEvent`](Types.md#systemevent) — A system event sent to the game (C: PDSystemEvent).
- [`Buttons`](Types.md#buttons) — The Playdate's buttons as bit flags (C: PDButtons, Lua: playdate.kButtonLeft ...).
- [`Language`](Types.md#language) — System language (C: PDLanguage).
- [`AccessReply`](Types.md#accessreply) — Immediate answer to a permission request (network, microphone).
- [`Peripherals`](Types.md#peripherals) — Optional sensors to switch on with `Sys.SetPeripheralsEnabled` (C: PDPeripherals).
- [`PowerStatus`](Types.md#powerstatus) — Power flags from `Sys.PowerStatus` (C: PDPowerStatus).
- [`PDDateTime`](Types.md#pddatetime) — A calendar date and time (C: struct PDDateTime).
- [`PDInfo`](Types.md#pdinfo) — System information from `Sys.SystemInfo` (C: struct PDInfo).
- [`FileOptions`](Types.md#fileoptions) — How to open a file (C: FileOptions).
- [`SeekOrigin`](Types.md#seekorigin) — Origin for `PDFile.Seek` (C: SEEK_SET, SEEK_CUR, SEEK_END).
- [`FileStat`](Types.md#filestat) — File information from `FileSystem.Stat` (C: FileStat).
- [`LCDRect`](Types.md#lcdrect) — An integer rectangle given by its edges (C: LCDRect).
- [`DrawMode`](Types.md#drawmode) — How bitmaps (and therefore text) are drawn (C: LCDBitmapDrawMode, Lua: playdate.graphics.kDrawMode...).
- [`BitmapFlip`](Types.md#bitmapflip) — Mirroring applied when drawing a bitmap (C: LCDBitmapFlip, Lua: playdate.graphics.kImageFlipped...).
- [`SolidColor`](Types.md#solidcolor) — The four solid colors (C: LCDSolidColor, Lua: playdate.graphics.kColorBlack ...).
- [`LineCapStyle`](Types.md#linecapstyle) — Line end style for `Graphics.SetLineCapStyle` (C: LCDLineCapStyle).
- [`StringEncoding`](Types.md#stringencoding) — Text encodings accepted by the C text functions (C: PDStringEncoding).
- [`PolygonFillRule`](Types.md#polygonfillrule) — Which areas of a self-intersecting polygon are filled (C: LCDPolygonFillRule).
- [`TextWrappingMode`](Types.md#textwrappingmode) — Line breaking for `Graphics.DrawTextInRect` (C: PDTextWrappingMode).
- [`TextAlignment`](Types.md#textalignment) — Horizontal alignment for `Graphics.DrawTextInRect` (C: PDTextAlignment).
- [`CollisionResponseType`](Types.md#collisionresponsetype) — How a moving sprite reacts when it hits another one (C: SpriteCollisionResponseType, Lua: playdate.graphics.sprite.kCollisionType...).
- [`PDRect`](Types.md#pdrect) — A float rectangle given by position and size (C: PDRect).
- [`CollisionPoint`](Types.md#collisionpoint) — A point in a collision result (C: CollisionPoint).
- [`CollisionVector`](Types.md#collisionvector) — A collision normal; each component is usually -1, 0 or 1 (C: CollisionVector).
- [`SpriteCollisionInfo`](Types.md#spritecollisioninfo) — One collision reported by `Sprite.MoveWithCollisions` or `Sprite.CheckCollisions` (C: SpriteCollisionInfo).
- [`SpriteQueryInfo`](Types.md#spritequeryinfo) — A sprite crossed by a line, from `Sprites.QuerySpriteInfoAlongLine` (C: SpriteQueryInfo).
- [`SoundFormat`](Types.md#soundformat) — Format of raw audio data (C: SoundFormat).
- [`LFOType`](Types.md#lfotype) — LFO wave shapes (C: LFOType).
- [`SoundWaveform`](Types.md#soundwaveform) — Synth waveforms (C: SoundWaveform, Lua: playdate.sound.kWave...).
- [`TwoPoleFilterType`](Types.md#twopolefiltertype) — Filter types for `SoundEffect.TwoPoleFilter` (C: TwoPoleFilterType).
- [`MicSource`](Types.md#micsource) — Microphone choice for `Sound.SetMicCallback` (C: enum MicSource).
- [`JsonValueType`](Types.md#jsonvaluetype) — JSON value kinds (C: json_value_type).
- [`JsonValueData`](Types.md#jsonvaluedata) — The payload of a `JsonValue` (C: the json_value union).
- [`JsonValue`](Types.md#jsonvalue) — A value handed to `JsonHandler` callbacks (C: json_value).
- [`JsonDecoder`](Types.md#jsondecoder) — The raw C json_decoder struct.
- [`JsonReader`](Types.md#jsonreader) — The raw C json_reader: a function that fills a buffer and returns the byte count (0 at the end).
- [`JsonEncoder`](Types.md#jsonencoder) — The raw C json_encoder struct, filled in by `PD.Json->initEncoder`.
- [`LuaValueType`](Types.md#luavaluetype) — Type of a constant in a `LuaVal` table (C: the lua_val type enum).
- [`LuaType`](Types.md#luatype) — Type of a Lua value on the stack, from `Lua.GetArgType(int)` (C: enum LuaType).
- [`LuaReg`](Types.md#luareg) — One entry of a class's method table (C: lua_reg).
- [`LuaValData`](Types.md#luavaldata) — The value of a `LuaVal` constant.
- [`LuaVal`](Types.md#luaval) — A named constant added to a Lua class (C: lua_val).
- [`PDScore`](Types.md#pdscore) — A raw scoreboard score (C: PDScore).
- [`PDListScore`](Types.md#pdlistscore) — A score inside a `PDScoresList` (C: PDScore without the board).
- [`PDScoresList`](Types.md#pdscoreslist) — A raw list of scores (C: PDScoresList).
- [`PDBoard`](Types.md#pdboard) — A raw scoreboard description (C: PDBoard).
- [`PDBoardsList`](Types.md#pdboardslist) — A raw list of scoreboards (C: PDBoardsList).
- [`NetErr`](Types.md#neterr) — Network result codes (C: PDNetErr).
- [`WifiStatus`](Types.md#wifistatus) — Wi-Fi state from `Network.Status` (C: WifiStatus).

## [Raw C API (Playdate.Native)](Native.md)

- [`LCDBitmap`](Native.md#lcdbitmap) — Opaque C type `LCDBitmap`, only used through pointers.
- [`LCDBitmap.LCDBitmapTable`](Native.md#lcdbitmaplcdbitmaptable) — Opaque C type `LCDBitmapTable`, only used through pointers.
- [`LCDBitmapTable.LCDFont`](Native.md#lcdbitmaptablelcdfont) — Opaque C type `LCDFont`, only used through pointers.
- [`LCDFont.LCDFontData`](Native.md#lcdfontlcdfontdata) — Opaque C type `LCDFontData`, only used through pointers.
- [`LCDFontData.LCDFontPage`](Native.md#lcdfontdatalcdfontpage) — Opaque C type `LCDFontPage`, only used through pointers.
- [`LCDFontPage.LCDFontGlyph`](Native.md#lcdfontpagelcdfontglyph) — Opaque C type `LCDFontGlyph`, only used through pointers.
- [`LCDFontGlyph.LCDTileMap`](Native.md#lcdfontglyphlcdtilemap) — Opaque C type `LCDTileMap`, only used through pointers.
- [`LCDTileMap.LCDVideoPlayer`](Native.md#lcdtilemaplcdvideoplayer) — Opaque C type `LCDVideoPlayer`, only used through pointers.
- [`LCDVideoPlayer.LCDStreamPlayer`](Native.md#lcdvideoplayerlcdstreamplayer) — Opaque C type `LCDStreamPlayer`, only used through pointers.
- [`LCDStreamPlayer.HTTPConnection`](Native.md#lcdstreamplayerhttpconnection) — Opaque C type `HTTPConnection`, only used through pointers.
- [`HTTPConnection.TCPConnection`](Native.md#httpconnectiontcpconnection) — Opaque C type `TCPConnection`, only used through pointers.
- [`TCPConnection.FilePlayer`](Native.md#tcpconnectionfileplayer) — Opaque C type `FilePlayer`, only used through pointers.
- [`FilePlayer.PDMenuItem`](Native.md#fileplayerpdmenuitem) — Opaque C type `PDMenuItem`, only used through pointers.
- [`PDMenuItem.LCDSprite`](Native.md#pdmenuitemlcdsprite) — Opaque C type `LCDSprite`, only used through pointers.
- [`LCDSprite.SoundSource`](Native.md#lcdspritesoundsource) — Opaque C type `SoundSource`, only used through pointers.
- [`SoundSource.AudioSample`](Native.md#soundsourceaudiosample) — Opaque C type `AudioSample`, only used through pointers.
- [`AudioSample.SamplePlayer`](Native.md#audiosamplesampleplayer) — Opaque C type `SamplePlayer`, only used through pointers.
- [`SamplePlayer.PDSynthSignalValue`](Native.md#sampleplayerpdsynthsignalvalue) — Opaque C type `PDSynthSignalValue`, only used through pointers.
- [`PDSynthSignalValue.PDSynthSignal`](Native.md#pdsynthsignalvaluepdsynthsignal) — Opaque C type `PDSynthSignal`, only used through pointers.
- [`PDSynthSignal.PDSynthLFO`](Native.md#pdsynthsignalpdsynthlfo) — Opaque C type `PDSynthLFO`, only used through pointers.
- [`PDSynthLFO.PDSynthEnvelope`](Native.md#pdsynthlfopdsynthenvelope) — Opaque C type `PDSynthEnvelope`, only used through pointers.
- [`PDSynthEnvelope.PDSynth`](Native.md#pdsynthenvelopepdsynth) — Opaque C type `PDSynth`, only used through pointers.
- [`PDSynth.ControlSignal`](Native.md#pdsynthcontrolsignal) — Opaque C type `ControlSignal`, only used through pointers.
- [`ControlSignal.PDSynthInstrument`](Native.md#controlsignalpdsynthinstrument) — Opaque C type `PDSynthInstrument`, only used through pointers.
- [`PDSynthInstrument.SequenceTrack`](Native.md#pdsynthinstrumentsequencetrack) — Opaque C type `SequenceTrack`, only used through pointers.
- [`SequenceTrack.SoundSequence`](Native.md#sequencetracksoundsequence) — Opaque C type `SoundSequence`, only used through pointers.
- [`SoundSequence.TwoPoleFilter`](Native.md#soundsequencetwopolefilter) — Opaque C type `TwoPoleFilter`, only used through pointers.
- [`TwoPoleFilter.OnePoleFilter`](Native.md#twopolefilteronepolefilter) — Opaque C type `OnePoleFilter`, only used through pointers.
- [`OnePoleFilter.BitCrusher`](Native.md#onepolefilterbitcrusher) — Opaque C type `BitCrusher`, only used through pointers.
- [`BitCrusher.RingModulator`](Native.md#bitcrusherringmodulator) — Opaque C type `RingModulator`, only used through pointers.
- [`RingModulator.DelayLine`](Native.md#ringmodulatordelayline) — Opaque C type `DelayLine`, only used through pointers.
- [`DelayLine.DelayLineTap`](Native.md#delaylinedelaylinetap) — Opaque C type `DelayLineTap`, only used through pointers.
- [`DelayLineTap.Overdrive`](Native.md#delaylinetapoverdrive) — Opaque C type `Overdrive`, only used through pointers.
- [`Overdrive.SoundEffect`](Native.md#overdrivesoundeffect) — Opaque C type `SoundEffect`, only used through pointers.
- [`SoundEffect.SoundChannel`](Native.md#soundeffectsoundchannel) — Opaque C type `SoundChannel`, only used through pointers.
- [`SoundChannel.LuaUDObject`](Native.md#soundchannelluaudobject) — Opaque C type `LuaUDObject`, only used through pointers.
- [`LuaUDObject.SDFile`](Native.md#luaudobjectsdfile) — Opaque C type `SDFile`, only used through pointers.
- [`SDFile.PlaydateAPI`](Native.md#sdfileplaydateapi) — Mirror of the C `struct PlaydateAPI`.
- [`playdate_sys`](Native.md#playdate_sys) — Mirror of the C `struct playdate_sys`.
- [`playdate_file`](Native.md#playdate_file) — Mirror of the C `struct playdate_file`.
- [`playdate_video`](Native.md#playdate_video) — Mirror of the C `struct playdate_video`.
- [`playdate_videostream`](Native.md#playdate_videostream) — Mirror of the C `struct playdate_videostream`.
- [`playdate_tilemap`](Native.md#playdate_tilemap) — Mirror of the C `struct playdate_tilemap`.
- [`playdate_graphics`](Native.md#playdate_graphics) — Mirror of the C `struct playdate_graphics`.
- [`playdate_sprite`](Native.md#playdate_sprite) — Mirror of the C `struct playdate_sprite`.
- [`playdate_display`](Native.md#playdate_display) — Mirror of the C `struct playdate_display`.
- [`playdate_sound_source`](Native.md#playdate_sound_source) — Mirror of the C `struct playdate_sound_source`.
- [`playdate_sound_fileplayer`](Native.md#playdate_sound_fileplayer) — Mirror of the C `struct playdate_sound_fileplayer`.
- [`playdate_sound_sample`](Native.md#playdate_sound_sample) — Mirror of the C `struct playdate_sound_sample`.
- [`playdate_sound_sampleplayer`](Native.md#playdate_sound_sampleplayer) — Mirror of the C `struct playdate_sound_sampleplayer`.
- [`playdate_sound_signal`](Native.md#playdate_sound_signal) — Mirror of the C `struct playdate_sound_signal`.
- [`playdate_sound_lfo`](Native.md#playdate_sound_lfo) — Mirror of the C `struct playdate_sound_lfo`.
- [`playdate_sound_envelope`](Native.md#playdate_sound_envelope) — Mirror of the C `struct playdate_sound_envelope`.
- [`playdate_sound_synth`](Native.md#playdate_sound_synth) — Mirror of the C `struct playdate_sound_synth`.
- [`playdate_control_signal`](Native.md#playdate_control_signal) — Mirror of the C `struct playdate_control_signal`.
- [`playdate_sound_instrument`](Native.md#playdate_sound_instrument) — Mirror of the C `struct playdate_sound_instrument`.
- [`playdate_sound_track`](Native.md#playdate_sound_track) — Mirror of the C `struct playdate_sound_track`.
- [`playdate_sound_sequence`](Native.md#playdate_sound_sequence) — Mirror of the C `struct playdate_sound_sequence`.
- [`playdate_sound_effect_twopolefilter`](Native.md#playdate_sound_effect_twopolefilter) — Mirror of the C `struct playdate_sound_effect_twopolefilter`.
- [`playdate_sound_effect_onepolefilter`](Native.md#playdate_sound_effect_onepolefilter) — Mirror of the C `struct playdate_sound_effect_onepolefilter`.
- [`playdate_sound_effect_bitcrusher`](Native.md#playdate_sound_effect_bitcrusher) — Mirror of the C `struct playdate_sound_effect_bitcrusher`.
- [`playdate_sound_effect_ringmodulator`](Native.md#playdate_sound_effect_ringmodulator) — Mirror of the C `struct playdate_sound_effect_ringmodulator`.
- [`playdate_sound_effect_delayline`](Native.md#playdate_sound_effect_delayline) — Mirror of the C `struct playdate_sound_effect_delayline`.
- [`playdate_sound_effect_overdrive`](Native.md#playdate_sound_effect_overdrive) — Mirror of the C `struct playdate_sound_effect_overdrive`.
- [`playdate_sound_effect`](Native.md#playdate_sound_effect) — Mirror of the C `struct playdate_sound_effect`.
- [`playdate_sound_channel`](Native.md#playdate_sound_channel) — Mirror of the C `struct playdate_sound_channel`.
- [`playdate_sound`](Native.md#playdate_sound) — Mirror of the C `struct playdate_sound`.
- [`playdate_lua`](Native.md#playdate_lua) — Mirror of the C `struct playdate_lua`.
- [`playdate_json`](Native.md#playdate_json) — Mirror of the C `struct playdate_json`.
- [`playdate_scoreboards`](Native.md#playdate_scoreboards) — Mirror of the C `struct playdate_scoreboards`.
- [`playdate_http`](Native.md#playdate_http) — Mirror of the C `struct playdate_http`.
- [`playdate_tcp`](Native.md#playdate_tcp) — Mirror of the C `struct playdate_tcp`.
- [`playdate_network`](Native.md#playdate_network) — Mirror of the C `struct playdate_network`.
