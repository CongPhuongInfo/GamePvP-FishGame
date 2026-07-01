Option Strict On
Option Explicit On

Imports System.Text
Imports System.Collections.Generic

Public Class FishGame

    Public Const POND_WIDTH As Integer = 760
    Public Const POND_HEIGHT As Integer = 520
    Public Const MAX_PLAYERS As Integer = 2
    Public Const TICK_FPS As Integer = 30   ' so tick/giay (khop voi TICK_MS = 33 trong Form)
    Public Const MAX_LEVEL As Integer = 5
    Public Const MAX_SIZE_LEVEL As Integer = 8     ' dung cho ca AI (van phan cap roi rac)
    Public Const MAX_SIZE As Single = 9.0!         ' dung cho kich thuoc lien tuc cua nguoi choi
    Public Const MIN_SIZE As Single = 1.0!
    Public Const EAT_MARGIN As Single = 0.35!      ' phai lon hon doi thu it nhat tung nay moi an duoc

    Public Enum ItemKind As Byte
        AiFish = 0
        Pearl = 1        ' ngoc trai: +diem, khong an duoc lan nhau
        Poison = 2       ' rong doc: an vao bi tru kich thuoc + diem
        ClockBonus = 3   ' dong ho: +thoi gian
        SpeedBonus = 4   ' tia chop: tang toc do boi tam thoi
    End Enum

    Public Structure Creature
        Public X As Single
        Public Y As Single
        Public Radius As Single
        Public SizeLevel As Integer    ' 1..MAX_SIZE_LEVEL, dung de so sanh an duoc hay khong (0 = vat pham, khong phai ca)
        Public DirX As Single
        Public DirY As Single
        Public Speed As Single
        Public Kind As ItemKind
        Public Active As Boolean
        Public WanderTimer As Integer  ' dem nguoc de doi huong boi ngau nhien
    End Structure

    ' === Trang thai tung nguoi choi (mang san index de mo rong PvP) ===
    Public PlayerX(MAX_PLAYERS - 1) As Single
    Public PlayerY(MAX_PLAYERS - 1) As Single
    Public PlayerSizeF(MAX_PLAYERS - 1) As Single
    Public PlayerScore(MAX_PLAYERS - 1) As Integer
    Public PlayerSpeedTimer(MAX_PLAYERS - 1) As Integer
    Public PlayerActive(MAX_PLAYERS - 1) As Boolean
    Public PlayerFacing(MAX_PLAYERS - 1) As Single   ' 1 = quay phai, -1 = quay trai (giu nguyen khi chi di chuyen doc)

    Public PlayerCount As Integer = 1

    Public Const BASE_PLAYER_SPEED As Single = 3.6!
    Public Const PLAYER_MIN_X As Single = 20.0!
    Public Const PLAYER_MAX_X As Single = POND_WIDTH - 20.0!
    Public Const PLAYER_MIN_Y As Single = 20.0!
    Public Const PLAYER_MAX_Y As Single = POND_HEIGHT - 20.0!

    Public Creatures As New List(Of Creature)()
    Private rng As New Random()

    Public TimeLeftFrames As Integer
    Public GameOver As Boolean
    Public LastLog As String
    Public TargetScore As Integer = 300
    Public Level As Integer = 1
    Public LevelCleared As Boolean = False   ' bao UI hien thong bao "len man" 1 lan

    Public Sub New()
        ResetGame(1)
    End Sub

    Public Sub ResetGame(playerCount As Integer)
        ResetGame(playerCount, 1)
    End Sub

    Public Sub ResetGame(playerCount As Integer, startLevel As Integer)
        PlayerCount = Math.Max(1, Math.Min(MAX_PLAYERS, playerCount))
        Level = Math.Max(1, Math.Min(MAX_LEVEL, startLevel))
        Creatures.Clear()
        GameOver = False
        LevelCleared = False
        LastLog = "Man " & Level.ToString() & " - Dung WASD / mui ten de boi, an ca nho hon ban!"
        TargetScore = GetTargetScoreForLevel(Level)
        TimeLeftFrames = GetRoundSecondsForLevel(Level) * TICK_FPS

        Dim i As Integer
        For i = 0 To MAX_PLAYERS - 1
            PlayerActive(i) = (i < PlayerCount)
            PlayerScore(i) = 0
            PlayerSizeF(i) = 2.0!
            PlayerSpeedTimer(i) = 0
            PlayerFacing(i) = 1.0!
            If PlayerCount = 1 Then
                PlayerX(i) = POND_WIDTH / 2.0!
            Else
                PlayerX(i) = POND_WIDTH * (0.3F + 0.4F * i)
            End If
            PlayerY(i) = POND_HEIGHT / 2.0!
        Next i

        SpawnInitialCreatures()
    End Sub

    ' ===== Cau hinh do kho theo tung man =====
    Private Function GetTargetScoreForLevel(lv As Integer) As Integer
        Select Case lv
            Case 1 : Return 300
            Case 2 : Return 500
            Case 3 : Return 800
            Case 4 : Return 1150
            Case Else : Return 1550
        End Select
    End Function

    Private Function GetRoundSecondsForLevel(lv As Integer) As Integer
        Select Case lv
            Case 1 : Return 90
            Case 2 : Return 80
            Case 3 : Return 70
            Case 4 : Return 65
            Case Else : Return 60
        End Select
    End Function

    Private Function GetAiFishCountForLevel() As Integer
        Return 14 + (Level - 1) * 4
    End Function

    Private Sub SpawnInitialCreatures()
        Dim count As Integer = GetAiFishCountForLevel()
        Dim i As Integer
        For i = 1 To count
            Creatures.Add(MakeRandomAiFish())
        Next i

        For i = 1 To 4
            Creatures.Add(MakeBonusItem(ItemKind.Pearl))
        Next i

        Dim poisonCount As Integer = 2 + (Level - 1)
        For i = 1 To poisonCount
            Creatures.Add(MakeBonusItem(ItemKind.Poison))
        Next i

        Creatures.Add(MakeBonusItem(ItemKind.ClockBonus))
        Creatures.Add(MakeBonusItem(ItemKind.SpeedBonus))
    End Sub

    Private Function MakeRandomAiFish() As Creature
        Dim it As New Creature()
        Dim roll As Integer = rng.Next(100)
        ' Cang len man cao, ca to xuat hien nhieu hon (kho an hon)
        Dim bigBonus As Integer = (Level - 1) * 5

        If roll < 45 - bigBonus Then
            it.SizeLevel = 1
        ElseIf roll < 75 - bigBonus Then
            it.SizeLevel = 2
        ElseIf roll < 92 Then
            it.SizeLevel = 3
        Else
            it.SizeLevel = 4 + rng.Next(0, 2) + (Level - 1)   ' ca to / sieu to, hiem hon
        End If
        it.SizeLevel = Math.Max(1, Math.Min(MAX_SIZE_LEVEL, it.SizeLevel))

        it.Kind = ItemKind.AiFish
        it.Radius = 8.0! + it.SizeLevel * 4.0!
        it.Speed = Math.Max(0.8!, 2.6! - it.SizeLevel * 0.15!)
        RandomizeDirection(it)
        it.WanderTimer = rng.Next(30, 90)
        it.Active = True
        RandomizePosition(it)
        Return it
    End Function

    Private Function MakeBonusItem(k As ItemKind) As Creature
        Dim it As New Creature()
        it.Kind = k
        it.SizeLevel = 0
        it.Radius = 10.0!
        it.Speed = 0
        it.DirX = 0 : it.DirY = 0
        it.WanderTimer = 0
        it.Active = True
        RandomizePosition(it)
        Return it
    End Function

    Private Sub RandomizePosition(ByRef it As Creature)
        Dim tries As Integer = 0
        Do
            it.X = CSng(rng.Next(CInt(it.Radius) + 10, POND_WIDTH - CInt(it.Radius) - 10))
            it.Y = CSng(rng.Next(CInt(it.Radius) + 10, POND_HEIGHT - CInt(it.Radius) - 10))
            tries += 1
        Loop While tries < 20 AndAlso IsOverlapping(it)
    End Sub

    Private Sub RandomizeDirection(ByRef it As Creature)
        Dim ang As Double = rng.NextDouble() * Math.PI * 2.0
        it.DirX = CSng(Math.Cos(ang))
        it.DirY = CSng(Math.Sin(ang))
    End Sub

    Private Function IsOverlapping(candidate As Creature) As Boolean
        Dim i As Integer
        For i = 0 To Creatures.Count - 1
            If Not Creatures(i).Active Then Continue For
            Dim dx As Single = Creatures(i).X - candidate.X
            Dim dy As Single = Creatures(i).Y - candidate.Y
            Dim distSq As Single = dx * dx + dy * dy
            Dim minDist As Single = Creatures(i).Radius + candidate.Radius + 4.0!
            If distSq < minDist * minDist Then Return True
        Next i
        Return False
    End Function

    ' Goi khi nguoi choi bam WASD / mui ten. dx,dy la thanh phan huong (-1/0/1), se duoc chuan hoa.
    Public Function MovePlayer(player As Integer, dx As Single, dy As Single) As Boolean
        If GameOver Then Return False
        If player < 0 OrElse player >= MAX_PLAYERS OrElse Not PlayerActive(player) Then Return False
        If dx = 0 AndAlso dy = 0 Then Return False

        Dim len As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
        If len > 0 Then
            dx /= len
            dy /= len
        End If

        ' Cap nhat huong quay dau: chi doi khi co thanh phan ngang ro rang,
        ' giu nguyen huong cu khi chi boi len/xuong (tranh cai giat huong lien tuc)
        If dx > 0.001! Then
            PlayerFacing(player) = 1.0!
        ElseIf dx < -0.001! Then
            PlayerFacing(player) = -1.0!
        End If

        Dim speed As Single = BASE_PLAYER_SPEED
        If PlayerSpeedTimer(player) > 0 Then speed *= 1.6!
        ' Ca cang to boi cang cham, can bang loi the kich thuoc
        speed *= Math.Max(0.5!, 1.2! - PlayerSizeF(player) * 0.06!)

        Dim nx As Single = PlayerX(player) + dx * speed
        Dim ny As Single = PlayerY(player) + dy * speed
        nx = Math.Max(PLAYER_MIN_X, Math.Min(PLAYER_MAX_X, nx))
        ny = Math.Max(PLAYER_MIN_Y, Math.Min(PLAYER_MAX_Y, ny))
        PlayerX(player) = nx
        PlayerY(player) = ny
        Return True
    End Function

    Public Function GetPlayerRadius(player As Integer) As Single
        Return 9.0! + PlayerSizeF(player) * 4.2!
    End Function

    Public Sub Tick()
        If GameOver Then Return

        TimeLeftFrames -= 1
        If TimeLeftFrames <= 0 Then
            TimeLeftFrames = 0
            GameOver = True
            LastLog = BuildEndMessage()
            Return
        End If

        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If Not PlayerActive(p) Then Continue For
            If PlayerSpeedTimer(p) > 0 Then PlayerSpeedTimer(p) -= 1
        Next p

        UpdateCreatures()
        CheckEatCollisions()
    End Sub

    Private Sub UpdateCreatures()
        Dim i As Integer
        For i = 0 To Creatures.Count - 1
            Dim it As Creature = Creatures(i)
            If it.Kind <> ItemKind.AiFish Then Continue For

            it.WanderTimer -= 1
            If it.WanderTimer <= 0 Then
                RandomizeDirection(it)
                it.WanderTimer = rng.Next(40, 110)
            End If

            it.X += it.DirX * it.Speed
            it.Y += it.DirY * it.Speed

            If it.X < it.Radius Then
                it.X = it.Radius : it.DirX = -it.DirX
            End If
            If it.X > POND_WIDTH - it.Radius Then
                it.X = POND_WIDTH - it.Radius : it.DirX = -it.DirX
            End If
            If it.Y < it.Radius Then
                it.Y = it.Radius : it.DirY = -it.DirY
            End If
            If it.Y > POND_HEIGHT - it.Radius Then
                it.Y = POND_HEIGHT - it.Radius : it.DirY = -it.DirY
            End If

            Creatures(i) = it
        Next i
    End Sub

    Private Sub CheckEatCollisions()
        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If Not PlayerActive(p) Then Continue For
            Dim pr As Single = GetPlayerRadius(p)

            Dim i As Integer
            For i = 0 To Creatures.Count - 1
                If Not Creatures(i).Active Then Continue For
                Dim it As Creature = Creatures(i)
                Dim dx As Single = it.X - PlayerX(p)
                Dim dy As Single = it.Y - PlayerY(p)
                Dim dist As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
                If dist > pr + it.Radius Then Continue For

                Select Case it.Kind
                    Case ItemKind.AiFish
                        If PlayerSizeF(p) > CSng(it.SizeLevel) + EAT_MARGIN Then
                            EatAiFish(p, i)
                        ElseIf CSng(it.SizeLevel) > PlayerSizeF(p) + EAT_MARGIN Then
                            PlayerEatenByFish(p)
                        End If
                        ' bang nhau: khong xay ra gi, ca boi tranh nhau
                    Case ItemKind.Pearl
                        PlayerScore(p) += 80
                        LastLog = "Player " & (p + 1).ToString() & " nhat ngoc trai, +80 diem!"
                        RespawnCreature(i, ItemKind.Pearl)
                    Case ItemKind.Poison
                        ShrinkPlayer(p)
                        LastLog = "Player " & (p + 1).ToString() & " an phai rong doc, bi nho lai!"
                        RespawnCreature(i, ItemKind.Poison)
                    Case ItemKind.ClockBonus
                        TimeLeftFrames += 10 * TICK_FPS
                        LastLog = "Player " & (p + 1).ToString() & " nhat duoc Dong Ho +10s!"
                        RespawnCreature(i, ItemKind.ClockBonus)
                    Case ItemKind.SpeedBonus
                        PlayerSpeedTimer(p) = Math.Max(PlayerSpeedTimer(p), 6 * TICK_FPS)
                        LastLog = "Player " & (p + 1).ToString() & " duoc tang Toc Do!"
                        RespawnCreature(i, ItemKind.SpeedBonus)
                End Select
            Next i
        Next p

        ' PvP: ca lon nuot ca be giua 2 nguoi choi
        If PlayerCount = 2 AndAlso PlayerActive(0) AndAlso PlayerActive(1) Then
            Dim dx As Single = PlayerX(1) - PlayerX(0)
            Dim dy As Single = PlayerY(1) - PlayerY(0)
            Dim dist As Single = CSng(Math.Sqrt(dx * dx + dy * dy))
            Dim r0 As Single = GetPlayerRadius(0)
            Dim r1 As Single = GetPlayerRadius(1)
            If dist <= r0 + r1 Then
                If PlayerSizeF(0) > PlayerSizeF(1) + EAT_MARGIN Then
                    EatOpponent(0, 1)
                ElseIf PlayerSizeF(1) > PlayerSizeF(0) + EAT_MARGIN Then
                    EatOpponent(1, 0)
                End If
            End If
        End If

        CheckWinCondition()
    End Sub

    Private Sub EatAiFish(p As Integer, idx As Integer)
        Dim it As Creature = Creatures(idx)
        Dim points As Integer = it.SizeLevel * 40
        PlayerScore(p) += points
        ' Cang to thi moi lan an tang it hon (giam dan ti le, tranh ca khong lon vo han)
        Dim growth As Single = (0.22! + it.SizeLevel * 0.05!) * (1.0! - PlayerSizeF(p) / (MAX_SIZE * 1.4!))
        PlayerSizeF(p) = Math.Min(MAX_SIZE, PlayerSizeF(p) + Math.Max(0.04!, growth))
        LastLog = "Player " & (p + 1).ToString() & " an ca, +" & points.ToString() & " diem!"
        RespawnCreature(idx, ItemKind.AiFish)
    End Sub

    Private Sub PlayerEatenByFish(p As Integer)
        PlayerScore(p) = Math.Max(0, PlayerScore(p) - 30)
        ShrinkPlayer(p)
        LastLog = "Player " & (p + 1).ToString() & " bi ca lon an, -30 diem!"
        RespawnPlayer(p)
    End Sub

    Private Sub EatOpponent(winner As Integer, loser As Integer)
        Dim bonus As Integer = 100 + CInt(PlayerSizeF(loser) * 20)
        PlayerScore(winner) += bonus
        PlayerSizeF(winner) = Math.Min(MAX_SIZE, PlayerSizeF(winner) + 1.0!)
        LastLog = "Player " & (winner + 1).ToString() & " nuot Player " & (loser + 1).ToString() & ", +" & bonus.ToString() & " diem!"
        PlayerSizeF(loser) = Math.Max(MIN_SIZE, PlayerSizeF(loser) - 2.0!)
        PlayerScore(loser) = Math.Max(0, PlayerScore(loser) - 50)
        RespawnPlayer(loser)
    End Sub

    Private Sub ShrinkPlayer(p As Integer)
        PlayerSizeF(p) = Math.Max(MIN_SIZE, PlayerSizeF(p) - 0.6!)
    End Sub

    Private Sub RespawnPlayer(p As Integer)
        PlayerX(p) = CSng(rng.Next(CInt(PLAYER_MIN_X), CInt(PLAYER_MAX_X)))
        PlayerY(p) = CSng(rng.Next(CInt(PLAYER_MIN_Y), CInt(PLAYER_MAX_Y)))
    End Sub

    Private Sub RespawnCreature(idx As Integer, k As ItemKind)
        Dim newIt As Creature
        If k = ItemKind.AiFish Then
            newIt = MakeRandomAiFish()
        Else
            newIt = MakeBonusItem(k)
        End If
        Creatures(idx) = newIt
    End Sub

    Private Sub CheckWinCondition()
        If PlayerCount = 1 Then
            If PlayerScore(0) >= TargetScore Then
                If Level < MAX_LEVEL Then
                    AdvanceLevel()
                Else
                    GameOver = True
                    LastLog = "CHIEN THANG! Ban da pha dao voi " & PlayerScore(0).ToString() & " diem!"
                End If
            End If
        End If
    End Sub

    Private Sub AdvanceLevel()
        Dim keepScore As Integer = PlayerScore(0)
        Dim keepSize As Single = PlayerSizeF(0)
        Level += 1
        LevelCleared = True
        Creatures.Clear()
        TargetScore = GetTargetScoreForLevel(Level)
        TimeLeftFrames = GetRoundSecondsForLevel(Level) * TICK_FPS
        PlayerScore(0) = keepScore
        PlayerSizeF(0) = keepSize
        PlayerX(0) = POND_WIDTH / 2.0!
        PlayerY(0) = POND_HEIGHT / 2.0!
        SpawnInitialCreatures()
        LastLog = "LEN MAN " & Level.ToString() & "! Diem giu nguyen: " & keepScore.ToString()
    End Sub

    Private Function BuildEndMessage() As String
        If PlayerCount = 1 Then
            Return "Het gio! Diem cua ban: " & PlayerScore(0).ToString()
        Else
            If PlayerScore(0) > PlayerScore(1) Then
                Return "Het gio! Player 1 THANG voi " & PlayerScore(0).ToString() & " diem!"
            ElseIf PlayerScore(1) > PlayerScore(0) Then
                Return "Het gio! Player 2 THANG voi " & PlayerScore(1).ToString() & " diem!"
            Else
                Return "Het gio! HOA, ca 2 cung " & PlayerScore(0).ToString() & " diem!"
            End If
        End If
    End Function

    ' ============================================================
    '  SERIALIZE / DESERIALIZE cho PvP mang (host la nguon su that)
    ' ============================================================
    Public Function Serialize() As String
        Dim sb As New StringBuilder()
        Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

        sb.Append(PlayerCount.ToString()) : sb.Append("|")
        sb.Append(Level.ToString()) : sb.Append("|")
        sb.Append(TargetScore.ToString()) : sb.Append("|")
        sb.Append(TimeLeftFrames.ToString()) : sb.Append("|")
        sb.Append(If(GameOver, "1", "0")) : sb.Append("|")
        sb.Append(LastLog.Replace("|", " ").Replace(Chr(13), " ").Replace(Chr(10), " ")) : sb.Append("|")

        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            sb.Append(If(PlayerActive(p), "1", "0")) : sb.Append(",")
            sb.Append(PlayerX(p).ToString(inv)) : sb.Append(",")
            sb.Append(PlayerY(p).ToString(inv)) : sb.Append(",")
            sb.Append(PlayerSizeF(p).ToString(inv)) : sb.Append(",")
            sb.Append(PlayerScore(p).ToString()) : sb.Append(",")
            sb.Append(PlayerSpeedTimer(p).ToString()) : sb.Append(",")
            sb.Append(PlayerFacing(p).ToString(inv))
            If p < MAX_PLAYERS - 1 Then sb.Append(";")
        Next p
        sb.Append("|")

        Dim i As Integer
        For i = 0 To Creatures.Count - 1
            Dim it As Creature = Creatures(i)
            sb.Append(it.X.ToString(inv)) : sb.Append(",")
            sb.Append(it.Y.ToString(inv)) : sb.Append(",")
            sb.Append(it.Radius.ToString(inv)) : sb.Append(",")
            sb.Append(it.SizeLevel.ToString()) : sb.Append(",")
            sb.Append(it.DirX.ToString(inv)) : sb.Append(",")
            sb.Append(it.DirY.ToString(inv)) : sb.Append(",")
            sb.Append(it.Speed.ToString(inv)) : sb.Append(",")
            sb.Append(CInt(it.Kind).ToString()) : sb.Append(",")
            sb.Append(If(it.Active, "1", "0"))
            If i < Creatures.Count - 1 Then sb.Append(";")
        Next i

        Return sb.ToString()
    End Function

    Public Sub Deserialize(data As String)
        Dim parts As String() = data.Split("|"c)
        If parts.Length < 7 Then Return
        Dim inv As System.Globalization.CultureInfo = System.Globalization.CultureInfo.InvariantCulture

        Integer.TryParse(parts(0), PlayerCount)
        Integer.TryParse(parts(1), Level)
        Integer.TryParse(parts(2), TargetScore)
        Integer.TryParse(parts(3), TimeLeftFrames)
        GameOver = (parts(4) = "1")
        LastLog = parts(5)

        Dim playerEntries As String() = parts(6).Split(";"c)
        Dim p As Integer
        For p = 0 To MAX_PLAYERS - 1
            If p >= playerEntries.Length Then Exit For
            Dim hp As String() = playerEntries(p).Split(","c)
            If hp.Length >= 6 Then
                PlayerActive(p) = (hp(0) = "1")
                Single.TryParse(hp(1), System.Globalization.NumberStyles.Float, inv, PlayerX(p))
                Single.TryParse(hp(2), System.Globalization.NumberStyles.Float, inv, PlayerY(p))
                Single.TryParse(hp(3), System.Globalization.NumberStyles.Float, inv, PlayerSizeF(p))
                Integer.TryParse(hp(4), PlayerScore(p))
                Integer.TryParse(hp(5), PlayerSpeedTimer(p))
                If hp.Length >= 7 Then
                    Single.TryParse(hp(6), System.Globalization.NumberStyles.Float, inv, PlayerFacing(p))
                End If
            End If
        Next p

        Creatures.Clear()
        If parts.Length > 7 AndAlso parts(7).Length > 0 Then
            For Each entry As String In parts(7).Split(";"c)
                If entry.Length = 0 Then Continue For
                Dim ip As String() = entry.Split(","c)
                If ip.Length >= 9 Then
                    Dim it As New Creature()
                    Single.TryParse(ip(0), System.Globalization.NumberStyles.Float, inv, it.X)
                    Single.TryParse(ip(1), System.Globalization.NumberStyles.Float, inv, it.Y)
                    Single.TryParse(ip(2), System.Globalization.NumberStyles.Float, inv, it.Radius)
                    Integer.TryParse(ip(3), it.SizeLevel)
                    Single.TryParse(ip(4), System.Globalization.NumberStyles.Float, inv, it.DirX)
                    Single.TryParse(ip(5), System.Globalization.NumberStyles.Float, inv, it.DirY)
                    Single.TryParse(ip(6), System.Globalization.NumberStyles.Float, inv, it.Speed)
                    Dim kv As Integer = 0 : Integer.TryParse(ip(7), kv) : it.Kind = CType(kv, ItemKind)
                    it.Active = (ip(8) = "1")
                    Creatures.Add(it)
                End If
            Next
        End If
    End Sub

End Class
