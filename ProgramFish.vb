Option Strict On
Option Explicit On

Imports System
Imports System.Windows.Forms

Module ProgramFish

    <STAThread()>
    Sub Main()
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.Run(New FishForm())
    End Sub

End Module
