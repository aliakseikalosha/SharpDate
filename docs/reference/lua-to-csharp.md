# Coming from Lua

The Lua API names you know and their SharpDate equivalents. "—" means the feature exists only in
Lua (usually in CoreLibs, which is Lua code built on the C API); the last column says what to do
instead. `gfx` = `playdate.graphics`, `snd` = `playdate.sound`.

## The game loop and system

| Lua | C# |
|---|---|
| `playdate.update()` | `Game.Update()` (return `true` after drawing) |
| main.lua top-level code | `Game.Initialize()` |
| `playdate.gameWillTerminate`, `deviceWillSleep` | `OnEvent(SystemEvent.Terminate, ...)` |
| `playdate.gameWillPause`, `gameWillResume` | `OnEvent(SystemEvent.Pause / Resume, ...)` |
| `playdate.deviceWillLock`, `deviceDidUnlock` | `OnEvent(SystemEvent.Lock / Unlock, ...)` |
| `playdate.keyPressed`, `keyReleased` (Simulator) | `OnEvent(SystemEvent.KeyPressed / KeyReleased, keyCode)` |
| `print(...)` | `Sys.Log(text)` or `Console.WriteLine` |
| `error(...)` | `Sys.Error(text)` or `throw` |
| `playdate.drawFPS(x, y)` | `Sys.DrawFPS(x, y)` |
| `playdate.display.setRefreshRate`, `getRefreshRate` | `Display.RefreshRate` |
| `playdate.getFPS()` | `Display.FPS` |
| `playdate.display.setScale`, `getScale` | `Display.SetScale` (track the value yourself) |
| `playdate.display.setInverted`, `setMosaic`, `setFlipped`, `setOffset` | `Display.SetInverted`, `SetMosaic`, `SetFlipped`, `SetOffset` |
| `playdate.display.getWidth/getHeight` | `Display.Width`, `Display.Height` |
| `playdate.wait(ms)` | `Sys.Delay(ms)` |
| `playdate.stop()`, `playdate.start()` | `Lua.Stop()`, `Lua.Start()` (Lua games); C# games: stop drawing in `Update` |
| `playdate.restart(args)`, `playdate.argv` | `Sys.RestartGame(args)`, `Sys.GetLaunchArgs(out path)` |
| `playdate.getSystemLanguage()` | `Sys.Language` |
| `playdate.getLocalizedText` / `playdate.graphics.getLocalizedText` | `Sys.GetLocalizedText(key)` |
| `playdate.getBatteryPercentage`, `getBatteryVoltage`, `getPowerStatus` | `Sys.BatteryPercentage`, `BatteryVoltage`, `PowerStatus` |
| `playdate.getFlipped`, `getReduceFlashing` | `Sys.Flipped`, `Sys.ReduceFlashing` |
| `playdate.setAutoLockDisabled` | `Sys.SetAutoLockDisabled` |
| `playdate.getSystemMenu():addMenuItem` / `addCheckmarkMenuItem` / `addOptionsMenuItem` | `Sys.AddMenuItem` / `AddCheckmarkMenuItem` / `AddOptionsMenuItem` |
| `menuItem:setTitle`, `getValue`, `setValue`, `removeMenuItem` | `MenuItem.Title`, `Value`, `Checked`, `Remove()` |
| `playdate.setMenuImage` | `Sys.SetMenuImage` |
| `playdate.serialMessageReceived` | `Sys.SetSerialMessageCallback` |
| `playdate.getStats`, `setStatsInterval`, `playdate.simulator.*` | — (use `Sys.ElapsedTime` to profile) |
| `playdate.setCollectsGarbage`, `setMinimumGCTime`, `setGCScaling` | — (`GC.Collect()` collects now) |

## Input

| Lua | C# |
|---|---|
| `playdate.buttonIsPressed(b)` | `Sys.IsPressed(Buttons.X)` |
| `playdate.buttonJustPressed`, `buttonJustReleased` | `Sys.JustPressed`, `Sys.JustReleased` |
| `playdate.getButtonState()` | `Sys.GetButtonState(out current, out pushed, out released)` |
| `playdate.AButtonDown()` and the other button callbacks, `inputHandlers` | `Sys.SetButtonCallback(handler)` or polling |
| `playdate.kButtonA` ... | `Buttons.A`, `Buttons.B`, `Buttons.Up`, ... |
| `playdate.getCrankPosition()` | `Sys.CrankAngle` |
| `playdate.getCrankChange()` | `Sys.CrankChange` (one value; Lua also returns an accelerated change) |
| `playdate.getCrankTicks(n)` | — accumulate `CrankChange` and step every 360/n degrees (see [game.md](game.md)) |
| `playdate.isCrankDocked()`, `cranked`, `crankDocked`, `crankUndocked` | `Sys.IsCrankDocked` (poll it) |
| `playdate.setCrankSoundsDisabled` | `Sys.SetCrankSoundsDisabled` |
| `playdate.startAccelerometer`, `stopAccelerometer` | `Sys.SetPeripheralsEnabled(Peripherals.Accelerometer / None)` |
| `playdate.readAccelerometer()` | `Sys.GetAccelerometer(out x, out y, out z)` |
| `playdate.ui.crankIndicator` | — draw your own (an arc with `Graphics.DrawEllipse`) |
| `playdate.keyboard` | — |

## Time

| Lua | C# |
|---|---|
| `playdate.getCurrentTimeMilliseconds()` | `Sys.CurrentTimeMilliseconds` |
| `playdate.resetElapsedTime()`, `getElapsedTime()` | `Sys.ResetElapsedTime()`, `Sys.ElapsedTime` |
| `playdate.getSecondsSinceEpoch()` | `Sys.GetSecondsSinceEpoch(out ms)` |
| `playdate.getTime()`, `getGMTTime()` | `Sys.ConvertEpochToDateTime(Sys.SecondsSinceEpoch + Sys.TimezoneOffset)` (local) or without the offset (GMT) |
| `playdate.epochFromTime`, `timeFromEpoch` | `Sys.ConvertDateTimeToEpoch`, `Sys.ConvertEpochToDateTime` |
| `playdate.shouldDisplay24HourTime()` | `Sys.ShouldDisplay24HourTime` |
| `playdate.getServerTime` | `Sys.GetServerTime(callback)` |
| `playdate.timer`, `playdate.frameTimer` | — count frames or compare `CurrentTimeMilliseconds` |
| `playdate.graphics.animator`, `animation.loop`, `animation.blinker`, `easingFunctions` | — compute from time; e.g. ease-out quad: `t * (2 - t)` for t 0-1 |

## Graphics

| Lua | C# |
|---|---|
| `gfx.clear(color)` | `Graphics.Clear(color)` |
| `gfx.setColor`, `setBackgroundColor` | pass the color to each call; `Graphics.SetBackgroundColor` |
| `gfx.setPattern`, `setDitherPattern(alpha)` | `new Pattern(...)`, `Pattern.Gray50` (pass it as the color) |
| `gfx.setLineWidth` | the `width` / `lineWidth` argument of `DrawLine`, `DrawEllipse`, `DrawRoundRect` |
| `gfx.setLineCapStyle` | `Graphics.SetLineCapStyle` |
| `gfx.setStrokeLocation` | — (ellipses and round rects are always stroked inside) |
| `gfx.drawLine`, `drawRect`, `fillRect` | `Graphics.DrawLine`, `DrawRect`, `FillRect` |
| `gfx.drawRoundRect`, `fillRoundRect` | `Graphics.DrawRoundRect`, `FillRoundRect` |
| `gfx.drawCircleAtPoint(x, y, r)`, `fillCircleAtPoint` | `DrawEllipse(x - r, y - r, 2 * r, 2 * r, lineWidth, 0, 0, color)`, `FillEllipse(...)` |
| `gfx.drawEllipseInRect`, `fillEllipseInRect`, `drawArc` | `Graphics.DrawEllipse` / `FillEllipse` (angles for arcs) |
| `gfx.drawTriangle`, `drawPolygon` | three / n `DrawLine` calls |
| `gfx.fillTriangle`, `fillPolygon` | `Graphics.FillTriangle`, `FillPolygon` |
| `gfx.drawPixel` | `Graphics.SetPixel` |
| `gfx.setImageDrawMode` | `Graphics.SetDrawMode` |
| `gfx.setDrawOffset`, `getDrawOffset` | `Graphics.SetDrawOffset` (remember the values) |
| `gfx.setClipRect`, `setScreenClipRect`, `clearClipRect` | `Graphics.SetClipRect`, `SetScreenClipRect`, `ClearClipRect` |
| `gfx.setStencilImage`, `clearStencil` | `Graphics.SetStencilImage`, `ClearStencil` |
| `gfx.setStencilPattern` | a stencil bitmap filled with a pattern, or `Sprite.SetStencilPattern` |
| `gfx.pushContext(image)`, `popContext()` | `Graphics.PushContext(bitmap)`, `PopContext()` |
| `gfx.lockFocus`, `unlockFocus` | `PushContext` / `PopContext` |
| `gfx.getDisplayImage()`, `getWorkingImage()` | `Graphics.GetDisplayBufferBitmap()`, `CopyFrameBufferBitmap()` |
| `gfx.checkAlphaCollision` | `Graphics.CheckMaskCollision` |
| `gfx.perlin`, `generateQRCode`, `imageWithText`, `nineSlice` | — |

## Text

| Lua | C# |
|---|---|
| `gfx.drawText(text, x, y)` | `Graphics.DrawText(text, x, y)` |
| `gfx.drawTextAligned(text, x, y, align)` | measure with `font.GetTextWidth` and offset x, or `DrawTextInRect` |
| `gfx.drawTextInRect(text, x, y, w, h, leading, truncator, align)` | `Graphics.DrawTextInRect(text, x, y, w, h, wrap, align)` (no truncation string) |
| `gfx.getTextSize(text)` | `font.GetTextWidth(text)` and `font.Height` (`GetTextHeightForMaxWidth` for wrapped text) |
| `gfx.font.new(path)`, `gfx.setFont(font)` | `Font.Load(path)`, `Graphics.SetFont(font)` |
| `font:getHeight()`, `getTextWidth()` | `font.Height`, `font.GetTextWidth(text)` |
| `font:setTracking`, `setLeading` | `Graphics.SetTextTracking`, `SetTextLeading` |
| `gfx.font.newFamily`, `*bold*` / `_italic_` markup | — load each font and switch with `SetFont` |
| `gfx.drawLocalizedText` | `Graphics.DrawText(Sys.GetLocalizedText(key), x, y)` |

## Images

| Lua | C# |
|---|---|
| `gfx.image.new(path)` | `Bitmap.Load(path)` |
| `gfx.image.new(w, h, bg)` | `Bitmap.Create(w, h, color)` |
| `image:draw(x, y, flip)`, `drawAnchored` | `bitmap.Draw(x, y, flip)` (anchor: subtract a fraction of the size) |
| `image:drawCentered(x, y)` | `bitmap.Draw(x - bitmap.Width / 2, y - bitmap.Height / 2)` |
| `image:drawScaled`, `drawRotated` | `bitmap.DrawScaled`, `DrawRotated` |
| `image:scaledImage`, `rotatedImage` | `bitmap.Rotated(0, sx, sy)`, `bitmap.Rotated(angle)` |
| `image:drawTiled` | `Graphics.TileBitmap` |
| `image:getSize()` | `bitmap.Width`, `bitmap.Height` |
| `image:copy()`, `clear(color)` | `bitmap.Copy()`, `bitmap.Clear(color)` |
| `image:sample(x, y)` | `bitmap.GetPixel(x, y)` |
| `image:getMaskImage`, `setMaskImage` | `bitmap.Mask` |
| `image:addMask`, `removeMask`, `hasMask` | create a mask bitmap and set `bitmap.Mask` / check `Mask.IsNull` |
| `image:setInverted`, `invertedImage`, `fadedImage`, `blurredImage`, `vcrPauseFilterImage`, `drawBlurred` | — draw with `DrawMode.Inverted`, patterns or your own frame-buffer code |
| `gfx.imagetable.new(path)` | `BitmapTable.Load(path)` |
| `imagetable:getImage(n)` (1-based) | `table[n - 1]` (0-based) |
| `imagetable:getLength()` | `table.Count` |
| `gfx.tilemap.new()` and its methods | `TileMap.Create()`, `ImageTable`, `SetSize`, `SetTiles`, `SetTile`, `GetTile`, `Draw` (indexes are 0-based) |
| `gfx.video.new(path)` and its methods | `VideoPlayer.Load(path)`, `RenderFrame`, `GetInfo`, `SetContext` |

## Sprites

| Lua | C# |
|---|---|
| `gfx.sprite.new(image)` | `new MySprite()` (a `Sprite` subclass) and `Image = image` |
| `sprite:add()`, `remove()` | `Add()`, `Remove()` (`Dispose()` to free) |
| `sprite:update()` (override) | `protected override void Update()` |
| `sprite:draw(x, y, w, h)` (override) | `HasDrawFunction => true` + `Draw(bounds, drawRect)` |
| `sprite:collisionResponse(other)` | `HasCollisionResponse => true` + `CollisionResponse(other)` |
| `gfx.sprite.update()` | `Sprites.UpdateAndDrawSprites()` |
| `sprite:moveTo`, `moveBy`, `getPosition` | `MoveTo`, `MoveBy`, `GetPosition`, `X`, `Y` |
| `sprite:setImage`, `getImage` | `Image`, `SetImage(image, flip)` |
| `sprite:setCenter`, `getCenter` | `SetCenter`, `GetCenter` |
| `sprite:setSize`, `setBounds`, `getBounds` | `SetSize`, `Bounds` |
| `sprite:setZIndex`, `setVisible`, `setOpaque`, `setUpdatesEnabled`, `setIgnoresDrawOffset` | `ZIndex`, `Visible`, `Opaque`, `UpdatesEnabled`, `IgnoresDrawOffset` |
| `sprite:setImageDrawMode`, `setImageFlip` | `DrawMode`, `ImageFlip` |
| `sprite:setTag`, `getTag` | `Tag` |
| `sprite:setClipRect`, `setStencilImage`, `setStencilPattern` | `SetClipRect`, `SetStencilImage`, `SetStencilPattern` |
| `sprite:markDirty`, `addDirtyRect` | `MarkDirty`, `Sprites.AddDirtyRect` |
| `sprite:setCollideRect`, `clearCollideRect`, `setCollisionsEnabled` | `CollideRect`, `ClearCollideRect()`, `CollisionsEnabled` |
| `sprite:moveWithCollisions(x, y)` | `MoveWithCollisions(x, y, out ax, out ay)` (dispose the list) |
| `sprite:checkCollisions` | `CheckCollisions` |
| `sprite:overlappingSprites()`, `gfx.sprite.allOverlappingSprites()` | `OverlappingSprites()`, `Sprites.AllOverlappingSprites()` |
| `gfx.sprite.querySpritesAtPoint`, `InRect`, `AlongLine`, `querySpriteInfoAlongLine` | `Sprites.QuerySprites...` |
| `gfx.sprite.kCollisionTypeSlide` ... | `CollisionResponseType.Slide` ... |
| `gfx.sprite.setAlwaysRedraw`, `spriteCount`, `removeAll` | `Sprites.SetAlwaysRedraw`, `Sprites.Count`, `Sprites.RemoveAllSprites` |
| `gfx.sprite.setBackgroundDrawingCallback` | a full-screen sprite with `ZIndex = short.MinValue` and a custom `Draw` (see [sprites.md](sprites.md)) |
| `sprite:setGroups`, `setCollidesWithGroups`, `setGroupMask` | — use `Tag` and decide in `CollisionResponse` |
| `gfx.sprite.getAllSprites()` | — keep your own `List<Sprite>` |
| `sprite:setAnimator`, `setRedrawsOnImageChange`, `gfx.sprite.spriteWithText` | — |

## Sound

| Lua | C# |
|---|---|
| `snd.fileplayer.new(path)` | `FilePlayer.Load(path)` |
| `fileplayer:play(n)`, `stop`, `pause`, `isPlaying` | `Play(n)`, `Stop()`, `Pause()`, `IsPlaying` |
| `fileplayer:setVolume`, `setRate`, `setOffset`, `setLoopRange`, `setFinishCallback` | `SetVolume`, `Rate`, `Offset`, `SetLoopRange`, `SetFinishCallback` |
| `snd.sample.new(path)` | `AudioSample.Load(path)` |
| `snd.sampleplayer.new(sample or path)` | `SamplePlayer.Create(sample)` |
| `sampleplayer:play(n, rate)`, `playAt`, `setPlayRange`, `setPaused` | `Play(n, rate)`, `SetPlayRange`, `SetPaused` (schedule with a synth's `when` instead of `playAt`) |
| `snd.synth.new(waveform)` | `Synth.Create(waveform)` |
| `synth:playNote(pitch, vol, len, when)`, `playMIDINote`, `noteOff` | `PlayNote(hz, velocity, length, when)`, `PlayMIDINote`, `NoteOff` |
| `synth:setADSR(a, d, s, r)` | `AttackTime`, `DecayTime`, `SustainLevel`, `ReleaseTime` |
| `synth:setFrequencyMod`, `setAmplitudeMod`, `setParameter`, `setParameterMod` | `FrequencyModulator`, `AmplitudeModulator`, `SetParameter`, `SetParameterModulator` |
| `snd.instrument.new()`, `addVoice` | `Instrument.Create()`, `AddVoice` |
| `snd.sequence.new(path)` | `SoundSequence.Create()` + `LoadMIDIFile(path)` |
| `snd.track.new()`, `addNote`, `setInstrument` | `SequenceTrack.Create()`, `AddNoteEvent`, `Instrument` |
| `snd.lfo.new(type)`, `snd.envelope.new(a, d, s, r)`, `snd.controlsignal.new()` | `LFO.Create`, `Envelope.Create`, `ControlSignal.Create` |
| `snd.channel.new()`, `addSource`, `addEffect`, `setVolume`, `setPan` | `SoundChannel.Create()` + `Sound.AddChannel`, `AddSource`, `AddEffect`, `Volume`, `Pan` |
| `snd.twopolefilter.new(type)`, `onepolefilter`, `bitcrusher`, `ringmod`, `overdrive`, `delayline` | `SoundEffect.TwoPoleFilter`, `OnePoleFilter`, `BitCrusher`, `RingModulator`, `Overdrive`, `DelayLine` |
| `delayline:addTap(delay)` | `effect.AddDelayTap(samples)` |
| `snd.micinput.startListening`, `getLevel` | `Sound.RequestMicAccess`, `Sound.SetMicCallback` (compute the level yourself) |
| `snd.micinput.recordToSample` | — collect samples in the mic callback |
| `snd.getCurrentTime()` | `Sound.CurrentTime` (samples, not seconds) |
| `snd.getHeadphoneState`, `setOutputsActive` | `Sound.GetHeadphoneState`, `Sound.SetOutputsActive` |
| `snd.playingSources()` | — |

## Files and data

| Lua | C# |
|---|---|
| `playdate.file.open(path, mode)` | `FileSystem.Open(path, options)` |
| `file:read(n)`, `readline()`, `write(s)`, `seek`, `tell`, `close` | `PDFile.Read`, —, `Write`, `Seek`, `Tell`, `Dispose` (read whole text with `FileSystem.ReadAllText` and `Split('\n')` for lines) |
| `playdate.file.exists`, `isdir`, `getSize`, `modtime` | `FileSystem.Exists`, `Stat(path, out stat)` |
| `playdate.file.listFiles`, `mkdir`, `delete`, `rename` | `FileSystem.ListFiles`, `Mkdir`, `Unlink`, `Rename` |
| `playdate.file.run`, `load` | — |
| `playdate.datastore.write(table, name)`, `read(name)` | `JsonWriter` + `FileSystem.WriteAllText`, `Json.ParseFile` |
| `playdate.datastore.writeImage`, `readImage` | — |
| `json.decode`, `json.decodeFile` | `Json.Parse`, `Json.ParseFile` (or `Json.Decode` with a `JsonHandler` for streaming) |
| `json.encode`, `encodePretty`, `encodeToFile` | `JsonWriter` (`pretty: true`) + `FileSystem.WriteAllText` |

## Network and scoreboards

| Lua | C# |
|---|---|
| `playdate.network.http.new(server, port, ssl)` | `new HttpConnection(server, port, ssl)` (after `HttpConnection.RequestAccess`) |
| `http:get`, `post`, `query`, `read`, `getResponseStatus`, `setHeadersReadCallback` ... | `Get`, `Post`, `Query`, `Read`, `ResponseStatus`, `OnHeadersRead` ... |
| `playdate.network.tcp.new(server, port, ssl)` | `new TcpConnection(server, port, ssl)` + `Open(callback)` |
| `playdate.network.setEnabled`, `getStatus` | `Network.SetEnabled`, `Network.Status` |
| `playdate.scoreboards.addScore`, `getScores`, `getPersonalBest`, `getScoreboards` | `Scoreboards.AddScore`, `GetScores`, `GetPersonalBest`, `GetScoreboards` |

## Language features

| Lua | C# |
|---|---|
| `class("Name").extends(Parent)` (CoreLibs/object) | C# classes |
| tables as arrays / dictionaries | `List<T>`, `T[]`, `Dictionary<K, V>` |
| `playdate.geometry.point`, `vector2D`, `rect`, `lineSegment`, `polygon`, `affineTransform` | — your own structs; `PDRect`, `LCDRect`; 3D: Mini3D's `Point3D`, `Vector3D`, `Matrix3D` |
| `math.random`, `math.floor`, ... | `Random`, `MathF.Floor`, ... (use `float`) |
| `playdate.math.lerp` | `a + (b - a) * t` |
| `string.format`, `..` | interpolation `$"..."`, `+` |
| `table.insert`, `table.remove`, `#t` | `list.Add`, `list.RemoveAt`, `list.Count` |
| coroutines | — state machines in `Update` |
| `playdate.ui.gridview`, `playdate.pathfinder` | — |
