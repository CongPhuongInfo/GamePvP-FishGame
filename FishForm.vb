Option Strict On
Option Explicit On

Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms
Imports System.Collections.Generic

Public Class FishForm
    Inherits Form

    Private Const SIDE_W As Integer = 260
    Private Const TICK_MS As Integer = 33   ' ~30fps, khop voi FishGame.TICK_FPS
    Private Const DEFAULT_PORT As Integer = 9989

    Private game As FishGame
    Private animTime As Single = 0.0F
    Private gameTimer As System.Windows.Forms.Timer
    Private playerCount As Integer = 1
    Private startLevel As Integer = 1

    ' === Mang (PvP Online) ===
    Private peer As NetworkPeer
    Private isOnlineMode As Boolean = False
    Private isHost As Boolean = False
    Private localPlayer As Integer = 0   ' host = 0, client = 1

    ' === Panels ===
    Private pnlMode As Panel
    Private pnlLevelSelect As Panel
    Private pnlConnect As Panel
    Private txtPort As TextBox
    Private txtIP As TextBox
    Private lblStatus As Label

    Private boardPanel As DoubleBufferedPanel
    Private lblTime As Label
    Private lblLevel As Label
    Private lblLog As Label
    Private btnRestart As Button

    ' === Player card panels (giong pattern TankGame) ===
    Private pnlCard0 As Panel
    Private pnlCard1 As Panel

    ' === Chat (chi hien khi choi Online PvP) ===
    Private pnlChat As Panel
    Private lstChat As ListBox
    Private txtChatInput As TextBox
    Private btnSend As Button

    ' === Mau ca theo tung player ===
    Private playerColors() As Color = {Color.LimeGreen, Color.DeepSkyBlue}

    ' === Theo doi phim dang giu de di chuyen muot moi tick, thay vi phu thuoc auto-repeat cua OS ===
    Private heldKeys As New HashSet(Of Keys)()

    Public Sub New()
        InitUI()
    End Sub

    ' Style dong bo cho cac nut o man hinh menu / chon man / ket noi mang
    ' de tranh bi anh huong boi theme toi cua Windows (nut mac dinh bi chim vao nen den)
    Private Sub StyleMenuButton(btn As Button, Optional accent As Color = Nothing)
        If accent.IsEmpty Then accent = Color.SteelBlue
        Dim baseColor As Color = accent
        Dim hoverColor As Color = ControlPaint.Light(accent, 0.25F)
        Dim pressColor As Color = ControlPaint.Dark(accent, 0.1F)

        btn.FlatStyle = FlatStyle.Flat
        btn.FlatAppearance.BorderSize = 1
        btn.FlatAppearance.BorderColor = ControlPaint.Light(accent, 0.4F)
        btn.FlatAppearance.MouseOverBackColor = hoverColor
        btn.FlatAppearance.MouseDownBackColor = pressColor
        btn.BackColor = baseColor
        btn.ForeColor = Color.White
        btn.Font = New Font("Segoe UI", 10.0!, FontStyle.Bold)
        btn.Cursor = Cursors.Hand
        btn.UseVisualStyleBackColor = False
        btn.TextAlign = ContentAlignment.MiddleCenter
    End Sub

    Private Sub InitUI()
        Me.Text = "Ca Lon Nuot Ca Be - 2CongLC"
        Me.ClientSize = New Size(FishGame.POND_WIDTH + SIDE_W, FishGame.POND_HEIGHT)
        Me.FormBorderStyle = FormBorderStyle.FixedSingle
        Me.MaximizeBox = False
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.BackColor = Color.FromArgb(10, 30, 45)
        Me.KeyPreview = True
        AddHandler Me.KeyDown, AddressOf FishForm_KeyDown
        AddHandler Me.KeyUp, AddressOf FishForm_KeyUp
        AddHandler Me.Deactivate, Sub() heldKeys.Clear()   ' tranh ket dinh phim khi Alt+Tab ra khoi cua so

        gameTimer = New System.Windows.Forms.Timer()
        gameTimer.Interval = TICK_MS
        AddHandler gameTimer.Tick, AddressOf GameTimer_Tick

        BuildModePanel()
        BuildLevelSelectPanel()
        BuildConnectPanel()
        BuildBoardPanel()
        BuildSidePanel()
        BuildChatPanel()

        pnlLevelSelect.Visible = False
        pnlConnect.Visible = False
        SetGameControlsVisible(False)
    End Sub

    Private Sub SetGameControlsVisible(v As Boolean)
        boardPanel.Visible = v
        lblTime.Visible = v
        lblLevel.Visible = v
        pnlCard0.Visible = v
        pnlCard1.Visible = v AndAlso playerCount = 2
        lblLog.Visible = v
        btnRestart.Visible = v
        pnlChat.Visible = v AndAlso isOnlineMode
    End Sub

    ' ============================================================
    '  MAN HINH CHON CHE DO
    ' ============================================================
    Private Sub BuildModePanel()
        pnlMode = New Panel()
        pnlMode.Location = New Point(0, 0)
        pnlMode.Size = New Size(FishGame.POND_WIDTH + SIDE_W, FishGame.POND_HEIGHT)
        pnlMode.BackColor = Color.FromArgb(10, 30, 45)

        Dim lblTitle As New Label()
        lblTitle.Text = "CA LON NUOT CA BE"
        lblTitle.Font = New Font("Segoe UI", 22.0!, FontStyle.Bold)
        lblTitle.ForeColor = Color.White
        lblTitle.AutoSize = True
        lblTitle.Location = New Point(60, 60)
        pnlMode.Controls.Add(lblTitle)

        Dim btn1P As New Button()
        btn1P.Text = "Choi 1 nguoi"
        btn1P.Location = New Point(60, 150)
        btn1P.Size = New Size(240, 46)
        StyleMenuButton(btn1P, Color.SeaGreen)
        AddHandler btn1P.Click, Sub() ShowLevelSelect(1)
        pnlMode.Controls.Add(btn1P)

        Dim btn2POffline As New Button()
        btn2POffline.Text = "2 nguoi - cung may (ban phim)"
        btn2POffline.Location = New Point(60, 206)
        btn2POffline.Size = New Size(300, 46)
        StyleMenuButton(btn2POffline, Color.SeaGreen)
        AddHandler btn2POffline.Click, Sub() StartOfflineGame(2, 1)
        pnlMode.Controls.Add(btn2POffline)

        Dim btnHost As New Button()
        btnHost.Text = "Tao phong (Host)"
        btnHost.Location = New Point(60, 262)
        btnHost.Size = New Size(240, 46)
        StyleMenuButton(btnHost, Color.SteelBlue)
        AddHandler btnHost.Click, Sub() ShowConnectPanel(True)
        pnlMode.Controls.Add(btnHost)

        Dim btnJoin As New Button()
        btnJoin.Text = "Vao phong (Client)"
        btnJoin.Location = New Point(60, 318)
        btnJoin.Size = New Size(240, 46)
        StyleMenuButton(btnJoin, Color.SteelBlue)
        AddHandler btnJoin.Click, Sub() ShowConnectPanel(False)
        pnlMode.Controls.Add(btnJoin)

        Me.Controls.Add(pnlMode)
    End Sub

    Private Sub BuildLevelSelectPanel()
        pnlLevelSelect = New Panel()
        pnlLevelSelect.Location = New Point(0, 0)
        pnlLevelSelect.Size = New Size(FishGame.POND_WIDTH + SIDE_W, FishGame.POND_HEIGHT)
        pnlLevelSelect.BackColor = Color.FromArgb(10, 30, 45)

        Dim lbl As New Label()
        lbl.Text = "Chon man choi"
        lbl.Font = New Font("Segoe UI", 16.0!, FontStyle.Bold)
        lbl.ForeColor = Color.White
        lbl.AutoSize = True
        lbl.Location = New Point(60, 60)
        pnlLevelSelect.Controls.Add(lbl)

        Dim lv As Integer
        For lv = 1 To FishGame.MAX_LEVEL
            Dim captureLv As Integer = lv
            Dim btn As New Button()
            btn.Text = "Man " & lv.ToString()
            btn.Location = New Point(60, 110 + (lv - 1) * 54)
            btn.Size = New Size(170, 46)
            StyleMenuButton(btn, Color.SeaGreen)
            AddHandler btn.Click, Sub() StartOfflineGame(1, captureLv)
            pnlLevelSelect.Controls.Add(btn)
        Next lv

        Dim btnBack As New Button()
        btnBack.Text = "<< Quay lai"
        btnBack.Location = New Point(60, 110 + FishGame.MAX_LEVEL * 54 + 20)
        btnBack.Size = New Size(170, 38)
        StyleMenuButton(btnBack, Color.FromArgb(90, 90, 100))
        AddHandler btnBack.Click, Sub()
                                       pnlLevelSelect.Visible = False
                                       pnlMode.Visible = True
                                   End Sub
        pnlLevelSelect.Controls.Add(btnBack)

        Me.Controls.Add(pnlLevelSelect)
    End Sub

    Private Sub ShowLevelSelect(pCount As Integer)
        playerCount = pCount
        pnlMode.Visible = False
        pnlLevelSelect.Visible = True
    End Sub

    ' ============================================================
    '  MAN HINH KET NOI MANG
    ' ============================================================
    Private Sub BuildConnectPanel()
        pnlConnect = New Panel()
        pnlConnect.Location = New Point(0, 0)
        pnlConnect.Size = New Size(FishGame.POND_WIDTH + SIDE_W, FishGame.POND_HEIGHT)
        pnlConnect.BackColor = Color.FromArgb(10, 30, 45)

        Dim lblPort As New Label()
        lblPort.Text = "Port:"
        lblPort.ForeColor = Color.White
        lblPort.Location = New Point(60, 70)
        lblPort.AutoSize = True
        pnlConnect.Controls.Add(lblPort)

        txtPort = New TextBox()
        txtPort.Text = DEFAULT_PORT.ToString()
        txtPort.Location = New Point(60, 92)
        txtPort.Size = New Size(120, 24)
        pnlConnect.Controls.Add(txtPort)

        Dim lblIP As New Label()
        lblIP.Text = "Dia chi IP host (chi can khi vao phong):"
        lblIP.ForeColor = Color.White
        lblIP.Location = New Point(60, 130)
        lblIP.AutoSize = True
        pnlConnect.Controls.Add(lblIP)

        txtIP = New TextBox()
        txtIP.Text = "127.0.0.1"
        txtIP.Location = New Point(60, 152)
        txtIP.Size = New Size(160, 24)
        pnlConnect.Controls.Add(txtIP)

        Dim btnGo As New Button()
        btnGo.Text = "Ket noi"
        btnGo.Location = New Point(60, 200)
        btnGo.Size = New Size(150, 42)
        StyleMenuButton(btnGo, Color.SteelBlue)
        AddHandler btnGo.Click, AddressOf BtnGo_Click
        pnlConnect.Controls.Add(btnGo)

        Dim btnBack As New Button()
        btnBack.Text = "<< Quay lai"
        btnBack.Location = New Point(60, 252)
        btnBack.Size = New Size(150, 38)
        StyleMenuButton(btnBack, Color.FromArgb(90, 90, 100))
        AddHandler btnBack.Click, Sub()
                                       pnlConnect.Visible = False
                                       pnlMode.Visible = True
                                   End Sub
        pnlConnect.Controls.Add(btnBack)

        lblStatus = New Label()
        lblStatus.ForeColor = Color.Orange
        lblStatus.Location = New Point(60, 300)
        lblStatus.AutoSize = True
        pnlConnect.Controls.Add(lblStatus)

        Me.Controls.Add(pnlConnect)
    End Sub

    Private Sub ShowConnectPanel(asHost As Boolean)
        isHost = asHost
        pnlMode.Visible = False
        pnlConnect.Visible = True
        lblStatus.Text = If(asHost, "San sang tao phong. Bam Ket noi de mo cong cho.", "Nhap IP host roi bam Ket noi.")
    End Sub

    Private Sub BtnGo_Click(sender As Object, e As EventArgs)
        Dim port As Integer
        If Not Integer.TryParse(txtPort.Text, port) Then
            lblStatus.Text = "Port khong hop le."
            Return
        End If

        isOnlineMode = True
        playerCount = 2
        peer = New NetworkPeer(Me)
        AddHandler peer.LineReceived, AddressOf Peer_LineReceived
        AddHandler peer.Disconnected, AddressOf Peer_Disconnected
        AddHandler peer.Connected, AddressOf Peer_Connected

        If isHost Then
            localPlayer = 0
            lblStatus.Text = "Dang cho doi thu ket noi..."
            peer.StartHost(port)
        Else
            localPlayer = 1
            lblStatus.Text = "Dang ket noi den " & txtIP.Text & "..."
            peer.ConnectToHost(txtIP.Text, port)
        End If
    End Sub

    Private Sub Peer_Connected()
        If isHost Then
            game = New FishGame()
            game.ResetGame(2, 1)
            ShowGamePanel()
            gameTimer.Start()
        Else
            lblStatus.Text = "Da ket noi! Cho host bat dau..."
        End If
    End Sub

    Private Sub Peer_Disconnected()
        gameTimer.Stop()
        MessageBox.Show("Mat ket noi voi doi thu.", "Mang")
        pnlConnect.Visible = True
        SetGameControlsVisible(False)
    End Sub

    Private Sub Peer_LineReceived(line As String)
        If line.StartsWith("STATE:") Then
            If game Is Nothing Then game = New FishGame()
            game.Deserialize(line.Substring(6))
            playerCount = game.PlayerCount
            If Not boardPanel.Visible Then ShowGamePanel()
            boardPanel.Invalidate()
            RefreshSide()
            If game.GameOver Then
                gameTimer.Stop()
                Me.BeginInvoke(New Action(Sub()
                                               MessageBox.Show(game.LastLog, "Ket thuc!")
                                           End Sub))
            End If

        ElseIf line.StartsWith("MOVE:") Then
            If isHost Then
                Dim parts() As String = line.Substring(5).Split(":"c)
                If parts.Length = 3 Then
                    Dim pIdx As Integer
                    Dim dx As Single, dy As Single
                    Integer.TryParse(parts(0), pIdx)
                    Single.TryParse(parts(1), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, dx)
                    Single.TryParse(parts(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, dy)
                    game.MovePlayer(pIdx, dx, dy)
                End If
            End If

        ElseIf line.StartsWith("CHAT:") Then
            Dim payload As String = line.Substring(5)
            Dim colon As Integer = payload.IndexOf(":"c)
            If colon >= 0 Then
                Dim tag As String = payload.Substring(0, colon)
                Dim msg As String = payload.Substring(colon + 1)
                AppendChat(tag & ": " & msg)
            End If
        End If
    End Sub

    Private Sub BroadcastState()
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("STATE:" & game.Serialize())
        End If
    End Sub

    ' ============================================================
    '  BAT DAU GAME OFFLINE
    ' ============================================================
    Private Sub StartOfflineGame(pCount As Integer, level As Integer)
        isOnlineMode = False
        playerCount = pCount
        startLevel = level
        game = New FishGame()
        game.ResetGame(pCount, level)
        heldKeys.Clear()
        ShowGamePanel()
        gameTimer.Start()
    End Sub

    Private Sub ShowGamePanel()
        pnlMode.Visible = False
        pnlLevelSelect.Visible = False
        pnlConnect.Visible = False
        SetGameControlsVisible(True)
        If lstChat IsNot Nothing Then lstChat.Items.Clear()
        boardPanel.Focus()
        RefreshSide()
        boardPanel.Invalidate()
    End Sub

    ' ============================================================
    '  BANG CHOI (VE GDI+)
    ' ============================================================
    Private Sub BuildBoardPanel()
        boardPanel = New DoubleBufferedPanel()
        boardPanel.Location = New Point(0, 0)
        boardPanel.Size = New Size(FishGame.POND_WIDTH, FishGame.POND_HEIGHT)
        boardPanel.BackColor = Color.FromArgb(15, 70, 110)
        AddHandler boardPanel.Paint, AddressOf BoardPanel_Paint
        Me.Controls.Add(boardPanel)
    End Sub

    Private Sub BuildSidePanel()
        Dim sideX As Integer = FishGame.POND_WIDTH + 16

        lblTime = New Label()
        lblTime.ForeColor = Color.White
        lblTime.Font = New Font("Segoe UI", 12.0!, FontStyle.Bold)
        lblTime.Location = New Point(sideX, 10)
        lblTime.AutoSize = True
        Me.Controls.Add(lblTime)

        lblLevel = New Label()
        lblLevel.ForeColor = Color.LightGray
        lblLevel.Location = New Point(sideX, 36)
        lblLevel.Size = New Size(SIDE_W - 24, 30)
        Me.Controls.Add(lblLevel)

        pnlCard0 = BuildPlayerCard("PLAYER 1", Color.LimeGreen, New Point(sideX, 70))
        Me.Controls.Add(pnlCard0)

        pnlCard1 = BuildPlayerCard("PLAYER 2", Color.DeepSkyBlue, New Point(sideX, 116))
        Me.Controls.Add(pnlCard1)

        lblLog = New Label()
        lblLog.ForeColor = Color.Yellow
        lblLog.Location = New Point(sideX, 166)
        lblLog.Size = New Size(SIDE_W - 24, 40)
        Me.Controls.Add(lblLog)

        btnRestart = New Button()
        btnRestart.Text = "Choi lai"
        btnRestart.Location = New Point(sideX, FishGame.POND_HEIGHT - 42)
        btnRestart.Size = New Size(160, 34)
        StyleMenuButton(btnRestart, Color.SteelBlue)
        AddHandler btnRestart.Click, AddressOf BtnRestart_Click
        Me.Controls.Add(btnRestart)
    End Sub

    ' Card hien thi ten + thong tin (diem so, kich thuoc) cua tung nguoi choi - giong pattern TankGame
    Private Function BuildPlayerCard(title As String, accent As Color, loc As Point) As Panel
        Dim p As New Panel()
        p.Location = loc
        p.Size = New Size(SIDE_W - 24, 42)
        p.BackColor = Color.FromArgb(35, 35, 35)

        Dim bar As New Panel()
        bar.Location = New Point(0, 0)
        bar.Size = New Size(4, 42)
        bar.BackColor = accent
        p.Controls.Add(bar)

        Dim lblTitle As New Label()
        lblTitle.Text = title
        lblTitle.Font = New Font("Segoe UI", 9.0!, FontStyle.Bold)
        lblTitle.ForeColor = accent
        lblTitle.Location = New Point(12, 4)
        lblTitle.AutoSize = True
        p.Controls.Add(lblTitle)

        Dim lblStats As New Label()
        lblStats.Font = New Font("Segoe UI", 9.0!)
        lblStats.ForeColor = Color.LightGray
        lblStats.Location = New Point(12, 21)
        lblStats.AutoSize = True
        p.Controls.Add(lblStats)

        Return p
    End Function

    ' ============================================================
    '  KHUNG CHAT (chi dung khi choi Online PvP qua NetworkPeer)
    ' ============================================================
    Private Sub BuildChatPanel()
        Dim sideX As Integer = FishGame.POND_WIDTH + 16
        Dim chatW As Integer = SIDE_W - 24
        Dim chatY As Integer = 216
        Dim chatH As Integer = FishGame.POND_HEIGHT - 42 - chatY - 10

        pnlChat = New Panel()
        pnlChat.Location = New Point(sideX, chatY)
        pnlChat.Size = New Size(chatW, chatH)
        pnlChat.BackColor = Color.FromArgb(20, 20, 20)
        pnlChat.Visible = False

        lstChat = New ListBox()
        lstChat.Location = New Point(0, 0)
        lstChat.Size = New Size(chatW, chatH - 32)
        lstChat.BackColor = Color.FromArgb(35, 35, 35)
        lstChat.ForeColor = Color.LightGray
        lstChat.BorderStyle = BorderStyle.FixedSingle
        pnlChat.Controls.Add(lstChat)

        txtChatInput = New TextBox()
        txtChatInput.Location = New Point(0, chatH - 27)
        txtChatInput.Size = New Size(chatW - 55, 25)
        AddHandler txtChatInput.KeyDown, Sub(s As Object, ev As KeyEventArgs)
                                              If ev.KeyCode = Keys.Enter Then
                                                  BtnSend_Click(s, EventArgs.Empty)
                                                  ev.Handled = True
                                                  ev.SuppressKeyPress = True
                                              End If
                                          End Sub
        pnlChat.Controls.Add(txtChatInput)

        btnSend = New Button()
        btnSend.Text = "Gui"
        btnSend.Location = New Point(chatW - 50, chatH - 28)
        btnSend.Size = New Size(50, 27)
        btnSend.BackColor = Color.SteelBlue
        btnSend.ForeColor = Color.White
        btnSend.FlatStyle = FlatStyle.Flat
        AddHandler btnSend.Click, AddressOf BtnSend_Click
        pnlChat.Controls.Add(btnSend)

        Me.Controls.Add(pnlChat)
    End Sub

    Private Sub BtnSend_Click(sender As Object, e As EventArgs)
        If txtChatInput.Text.Trim() = "" Then Return
        Dim tag As String = If(localPlayer = 0, "Player 1", "Player 2")
        Dim msg As String = txtChatInput.Text.Trim()
        AppendChat(tag & ": " & msg)
        If peer IsNot Nothing AndAlso peer.IsConnected Then
            peer.SendLine("CHAT:" & tag & ":" & msg)
        End If
        txtChatInput.Text = ""
    End Sub

    Private Sub AppendChat(msg As String)
        lstChat.Items.Add(msg)
        lstChat.TopIndex = Math.Max(0, lstChat.Items.Count - 1)
    End Sub

    Private Sub BtnRestart_Click(sender As Object, e As EventArgs)
        gameTimer.Stop()
        heldKeys.Clear()
        SetGameControlsVisible(False)
        If peer IsNot Nothing Then
            peer.CloseConnection()
            peer = Nothing
        End If
        isOnlineMode = False
        pnlMode.Visible = True
    End Sub

    Private Sub GameTimer_Tick(sender As Object, e As EventArgs)
        animTime += 0.18F
        If game Is Nothing Then Return

        ApplyHeldMovement()   ' di chuyen deu dan moi tick thay vi phu thuoc auto-repeat cua OS

        If isOnlineMode AndAlso Not isHost Then
            boardPanel.Invalidate()
            Return   ' client chi nhan STATE, khong tu tick logic, nhung van ve lai de vay duoi
        End If

        game.Tick()
        boardPanel.Invalidate()
        RefreshSide()

        If isOnlineMode Then BroadcastState()

        If game.GameOver Then
            gameTimer.Stop()
            Me.BeginInvoke(New Action(Sub()
                                           MessageBox.Show(game.LastLog, "Ket thuc!")
                                       End Sub))
        End If
    End Sub

    Private Sub RefreshSide()
        If game Is Nothing Then Return
        Dim secLeft As Integer = Math.Max(0, game.TimeLeftFrames \ FishGame.TICK_FPS)
        lblTime.Text = "⏱ " & secLeft.ToString() & "s"
        lblTime.ForeColor = If(secLeft <= 10, Color.OrangeRed, Color.White)
        lblLevel.Text = "Man " & game.Level.ToString() & "/" & FishGame.MAX_LEVEL.ToString() & "   Muc tieu: " & game.TargetScore.ToString()

        Dim stats0 As Label = TryCast(pnlCard0.Controls(2), Label)
        If stats0 IsNot Nothing Then
            stats0.Text = game.PlayerScore(0).ToString() & " diem   Cap " & game.PlayerSizeF(0).ToString("0.0")
        End If

        pnlCard1.Visible = (playerCount = 2)
        If playerCount = 2 Then
            Dim stats1 As Label = TryCast(pnlCard1.Controls(2), Label)
            If stats1 IsNot Nothing Then
                stats1.Text = game.PlayerScore(1).ToString() & " diem   Cap " & game.PlayerSizeF(1).ToString("0.0")
            End If
        End If
        lblLog.Text = game.LastLog
    End Sub

    ' ============================================================
    '  INPUT (WASD cho Player 1, mui ten cho Player 2 khi 2 nguoi cung may,
    '  hoac cho ca Player 1 khi choi 1 minh)
    ' ============================================================
    ' WinForms mac dinh coi phim mui ten la "dialog navigation key" va Panel se nuot mat
    ' truoc khi toi duoc Form_KeyDown. Override ProcessCmdKey de chan bat truoc.
    ' Thay vi di chuyen ngay trong ProcessCmdKey (phu thuoc toc do auto-repeat cua OS,
    ' gay giat/khong deu), ta chi ghi nhan phim dang giu vao heldKeys, roi ApplyHeldMovement()
    ' se doc tap nay va di chuyen deu dan moi tick (33ms) trong GameTimer_Tick.
    Protected Overrides Function ProcessCmdKey(ByRef msg As Message, keyData As Keys) As Boolean
        If txtChatInput IsNot Nothing AndAlso txtChatInput.Focused Then
            Return MyBase.ProcessCmdKey(msg, keyData)
        End If
        Select Case keyData
            Case Keys.W, Keys.S, Keys.A, Keys.D, Keys.Up, Keys.Down, Keys.Left, Keys.Right
                heldKeys.Add(keyData)
                Return True
        End Select
        Return MyBase.ProcessCmdKey(msg, keyData)
    End Function

    Private Sub FishForm_KeyDown(sender As Object, e As KeyEventArgs)
        ' Du phong: WASD/mui ten da duoc xu ly qua ProcessCmdKey o tren.
    End Sub

    Private Sub FishForm_KeyUp(sender As Object, e As KeyEventArgs)
        heldKeys.Remove(e.KeyCode)
    End Sub

    ' Doc tap phim dang giu va di chuyen tung player tuong ung - goi moi tick de chuyen dong muot.
    Private Sub ApplyHeldMovement()
        If game Is Nothing OrElse game.GameOver Then Return

        ' Player 0 (local player khi online, hoac player 1 khi offline): luon dung WASD.
        Dim dx0 As Single = 0.0F
        Dim dy0 As Single = 0.0F
        If heldKeys.Contains(Keys.A) Then dx0 -= 1.0F
        If heldKeys.Contains(Keys.D) Then dx0 += 1.0F
        If heldKeys.Contains(Keys.W) Then dy0 -= 1.0F
        If heldKeys.Contains(Keys.S) Then dy0 += 1.0F

        ' Choi 1 minh: cho phep dung them mui ten de dieu khien ca duy nhat tren san.
        If playerCount = 1 AndAlso Not isOnlineMode Then
            If heldKeys.Contains(Keys.Left) Then dx0 -= 1.0F
            If heldKeys.Contains(Keys.Right) Then dx0 += 1.0F
            If heldKeys.Contains(Keys.Up) Then dy0 -= 1.0F
            If heldKeys.Contains(Keys.Down) Then dy0 += 1.0F
        End If

        If dx0 <> 0.0F OrElse dy0 <> 0.0F Then DoMove(0, dx0, dy0)

        ' Player 2: mui ten (offline 2 nguoi cung may, hoac client khi choi online).
        If (playerCount = 2 AndAlso Not isOnlineMode) OrElse (isOnlineMode AndAlso localPlayer = 1) Then
            Dim dx1 As Single = 0.0F
            Dim dy1 As Single = 0.0F
            If heldKeys.Contains(Keys.Left) Then dx1 -= 1.0F
            If heldKeys.Contains(Keys.Right) Then dx1 += 1.0F
            If heldKeys.Contains(Keys.Up) Then dy1 -= 1.0F
            If heldKeys.Contains(Keys.Down) Then dy1 += 1.0F
            If dx1 <> 0.0F OrElse dy1 <> 0.0F Then DoMove(1, dx1, dy1)
        End If
    End Sub

    ' Goi khi nguoi choi di chuyen: offline xu ly truc tiep, online gui lenh ve host
    Private Sub DoMove(player As Integer, dx As Single, dy As Single)
        If isOnlineMode Then
            If player <> localPlayer Then Return   ' chi dieu khien duoc ca cua chinh minh
            If isHost Then
                game.MovePlayer(player, dx, dy)
                boardPanel.Invalidate()
            Else
                Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture
                peer.SendLine("MOVE:" & player.ToString() & ":" & dx.ToString(inv) & ":" & dy.ToString(inv))
            End If
        Else
            game.MovePlayer(player, dx, dy)
            boardPanel.Invalidate()
        End If
    End Sub

    ' ============================================================
    '  VE BANG CHOI
    ' ============================================================
    Private Sub BoardPanel_Paint(sender As Object, e As PaintEventArgs)
        If game Is Nothing Then Return
        Dim g As Graphics = e.Graphics
        g.SmoothingMode = SmoothingMode.AntiAlias

        DrawBackground(g)

        Dim i As Integer
        For i = 0 To game.Creatures.Count - 1
            DrawCreature(g, game.Creatures(i))
        Next i

        Dim p As Integer
        For p = 0 To FishGame.MAX_PLAYERS - 1
            If Not game.PlayerActive(p) Then Continue For
            DrawPlayerFish(g, p)
        Next p

        Using infoBrush As New SolidBrush(Color.White)
            g.DrawString("Man " & game.Level.ToString() & " - Muc tieu " & game.TargetScore.ToString() & " diem", New Font("Segoe UI", 9.0!), infoBrush, 10, 8)
        End Using
    End Sub

    Private Sub DrawBackground(g As Graphics)
        Using waterBrush As New LinearGradientBrush(New Point(0, 0), New Point(0, FishGame.POND_HEIGHT), Color.FromArgb(20, 110, 160), Color.FromArgb(5, 35, 60))
            g.FillRectangle(waterBrush, 0, 0, FishGame.POND_WIDTH, FishGame.POND_HEIGHT)
        End Using
    End Sub

    Private Sub DrawCreature(g As Graphics, it As FishGame.Creature)
        If Not it.Active Then Return
        Select Case it.Kind
            Case FishGame.ItemKind.AiFish
                DrawFishShape(g, it.X, it.Y, it.Radius, it.DirX, GetAiFishColor(it.SizeLevel), animTime * 1.4F + it.X * 0.05F + it.Y * 0.03F)
            Case FishGame.ItemKind.Pearl
                Using b As New SolidBrush(Color.White)
                    g.FillEllipse(b, it.X - it.Radius * 0.6F, it.Y - it.Radius * 0.6F, it.Radius * 1.2F, it.Radius * 1.2F)
                End Using
                Using p As New Pen(Color.LightCyan, 1.5F)
                    g.DrawEllipse(p, it.X - it.Radius * 0.6F, it.Y - it.Radius * 0.6F, it.Radius * 1.2F, it.Radius * 1.2F)
                End Using
            Case FishGame.ItemKind.Poison
                Using b As New SolidBrush(Color.MediumPurple)
                    g.FillEllipse(b, it.X - it.Radius, it.Y - it.Radius, it.Radius * 2, it.Radius * 2)
                End Using
                DrawCenteredGlyph(g, "X", it.X, it.Y, Color.White)
            Case FishGame.ItemKind.ClockBonus
                Using b As New SolidBrush(Color.Gold)
                    g.FillEllipse(b, it.X - it.Radius, it.Y - it.Radius, it.Radius * 2, it.Radius * 2)
                End Using
                DrawCenteredGlyph(g, "T", it.X, it.Y, Color.Black)
            Case FishGame.ItemKind.SpeedBonus
                Using b As New SolidBrush(Color.Cyan)
                    g.FillEllipse(b, it.X - it.Radius, it.Y - it.Radius, it.Radius * 2, it.Radius * 2)
                End Using
                DrawCenteredGlyph(g, "S", it.X, it.Y, Color.Black)
        End Select
    End Sub

    Private Sub DrawCenteredGlyph(g As Graphics, txt As String, x As Single, y As Single, col As Color)
        Using f As New Font("Segoe UI", 9.0!, FontStyle.Bold)
            Using b As New SolidBrush(col)
                g.DrawString(txt, f, b, x - 5, y - 7)
            End Using
        End Using
    End Sub

    Private Function GetAiFishColor(sizeLevel As Integer) As Color
        Select Case sizeLevel
            Case 1 : Return Color.FromArgb(255, 220, 120)
            Case 2 : Return Color.FromArgb(255, 170, 80)
            Case 3 : Return Color.FromArgb(255, 120, 90)
            Case Else : Return Color.FromArgb(220, 60, 60)
        End Select
    End Function

    Private Sub DrawPlayerFish(g As Graphics, p As Integer)
        Dim r As Single = game.GetPlayerRadius(p)
        Dim col As Color = playerColors(p)
        Dim phase As Single = animTime + p * 1.7F
        DrawFishShape(g, game.PlayerX(p), game.PlayerY(p), r, game.PlayerFacing(p), col, phase, True)

        Using nameFont As New Font("Segoe UI", 8.0!, FontStyle.Bold)
            Using nameOutline As New SolidBrush(Color.FromArgb(160, 0, 0, 0))
                g.DrawString("P" & (p + 1).ToString(), nameFont, nameOutline, game.PlayerX(p) - 7, game.PlayerY(p) - r - 15)
            End Using
            Using nameBrush As New SolidBrush(Color.White)
                g.DrawString("P" & (p + 1).ToString(), nameFont, nameBrush, game.PlayerX(p) - 8, game.PlayerY(p) - r - 16)
            End Using
        End Using
    End Sub

    ' Ve mot con ca bang GDI+: bong do, than gradient, vay lung/vay bung, duoi vay dong theo thoi gian,
    ' vien dam, vet sang (highlight) tao chieu sau. Khong can file anh.
    Private Sub DrawFishShape(g As Graphics, x As Single, y As Single, r As Single, dirX As Single, col As Color, Optional phase As Single = 0.0F, Optional isPlayer As Boolean = False)
        Dim facingRight As Boolean = (dirX >= 0)
        Dim flip As Single = If(facingRight, 1.0F, -1.0F)
        Dim bodyW As Single = r * 2.0F
        Dim bodyH As Single = r * 1.3F

        ' --- Bong do duoi nuoc (lech xuong duoi mot chut, mo nhat) ---
        Using shadowBrush As New SolidBrush(Color.FromArgb(60, 0, 0, 0))
            g.FillEllipse(shadowBrush, x - bodyW / 2.0F, y - bodyH / 2.0F + r * 0.35F, bodyW, bodyH * 0.7F)
        End Using

        ' --- Duoi vay dong: goc lac theo sin(phase), bien do nho hon khi ca to (mem mai hon) ---
        Dim wagAngle As Single = CSng(Math.Sin(phase) * 0.5)
        Dim tailBaseX As Single = If(facingRight, x - bodyW / 2.0F, x + bodyW / 2.0F)
        Dim tailDir As Single = -flip
        Dim tailLen As Single = r * 0.95F
        Dim tailTipX As Single = tailBaseX + tailDir * tailLen * CSng(Math.Cos(wagAngle))
        Dim tailTipY As Single = y + tailLen * CSng(Math.Sin(wagAngle))
        Dim tailPts() As PointF = {
            New PointF(tailBaseX, y - 2.0F),
            New PointF(tailTipX, tailTipY - r * 0.45F),
            New PointF(tailBaseX + tailDir * r * 0.25F, y),
            New PointF(tailTipX, tailTipY + r * 0.45F),
            New PointF(tailBaseX, y + 2.0F)
        }
        Dim tailCol As Color = ShadeColor(col, -25)
        Using tailBrush As New SolidBrush(tailCol)
            g.FillPolygon(tailBrush, tailPts)
        End Using

        ' --- Vay lung (tam giac nho phia tren than) ---
        Dim finTop() As PointF = {
            New PointF(x - flip * bodyW * 0.05F, y - bodyH * 0.45F),
            New PointF(x, y - bodyH * 0.95F),
            New PointF(x + flip * bodyW * 0.22F, y - bodyH * 0.4F)
        }
        Using finBrush As New SolidBrush(ShadeColor(col, -15))
            g.FillPolygon(finBrush, finTop)
        End Using

        ' --- Vay bung (nho hon, phia duoi) ---
        Dim finBottom() As PointF = {
            New PointF(x - flip * bodyW * 0.02F, y + bodyH * 0.4F),
            New PointF(x + flip * bodyW * 0.05F, y + bodyH * 0.75F),
            New PointF(x + flip * bodyW * 0.2F, y + bodyH * 0.38F)
        }
        Using finBrush2 As New SolidBrush(ShadeColor(col, -15))
            g.FillPolygon(finBrush2, finBottom)
        End Using

        ' --- Than ca: gradient sang-toi de tao chieu sau (sang o tren, toi o duoi) ---
        Dim bodyRect As New RectangleF(x - bodyW / 2.0F, y - bodyH / 2.0F, bodyW, bodyH)
        Using bodyBrush As New LinearGradientBrush(bodyRect, ShadeColor(col, 35), ShadeColor(col, -30), LinearGradientMode.Vertical)
            g.FillEllipse(bodyBrush, bodyRect)
        End Using

        ' --- Vien than ---
        Using outlinePen As New Pen(ShadeColor(col, -45), If(isPlayer, 2.0F, 1.2F))
            g.DrawEllipse(outlinePen, bodyRect)
        End Using

        ' --- Vet sang nho (highlight) phia tren than, tao cam giac bong loang ---
        Dim hlW As Single = bodyW * 0.35F
        Dim hlH As Single = bodyH * 0.22F
        Using hlBrush As New SolidBrush(Color.FromArgb(90, 255, 255, 255))
            g.FillEllipse(hlBrush, x - flip * bodyW * 0.05F - hlW / 2.0F, y - bodyH * 0.32F, hlW, hlH)
        End Using

        ' --- Mat ca ---
        Dim eyeX As Single = x + flip * bodyW * 0.24F
        Dim eyeY As Single = y - bodyH * 0.12F
        Using eyeWhite As New SolidBrush(Color.White)
            g.FillEllipse(eyeWhite, eyeX - 3.2F, eyeY - 3.2F, 6.4F, 6.4F)
        End Using
        Using eyePen As New Pen(Color.FromArgb(120, 0, 0, 0), 0.8F)
            g.DrawEllipse(eyePen, eyeX - 3.2F, eyeY - 3.2F, 6.4F, 6.4F)
        End Using
        Using eyeBlack As New SolidBrush(Color.Black)
            g.FillEllipse(eyeBlack, eyeX - 1.6F, eyeY - 1.6F, 3.2F, 3.2F)
        End Using

        ' --- Vuong mien (vuong rang gai phia truoc, them net "ca") ---
        If isPlayer Then
            Dim crownX As Single = x + flip * bodyW * 0.0F
            Using crownPen As New Pen(ShadeColor(col, -50), 1.0F)
                g.DrawLine(crownPen, x, y - bodyH * 0.55F, x, y - bodyH * 0.78F)
            End Using
        End If
    End Sub

    ' Lam sang (delta > 0) hoac toi (delta < 0) mot mau, gioi han trong khoang 0-255
    Private Function ShadeColor(c As Color, delta As Integer) As Color
        Dim r As Integer = Math.Max(0, Math.Min(255, CInt(c.R) + delta))
        Dim g As Integer = Math.Max(0, Math.Min(255, CInt(c.G) + delta))
        Dim b As Integer = Math.Max(0, Math.Min(255, CInt(c.B) + delta))
        Return Color.FromArgb(c.A, r, g, b)
    End Function

End Class

' ============================================================
'  PANEL VE DOUBLE-BUFFER (chong giat hinh khi Invalidate lien tuc)
' ============================================================
Public Class DoubleBufferedPanel
    Inherits Panel

    Public Sub New()
        Me.SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or ControlStyles.OptimizedDoubleBuffer, True)
        Me.UpdateStyles()
    End Sub
End Class
