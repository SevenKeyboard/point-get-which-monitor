#Requires AutoHotkey v2.0.0+
#Include "%A_ScriptDir%"
#Include ".\lib\MonitorExGetUtils.ahk"
;==============================================================
; pointGetWhichMonitor — Determine monitor index from a point
;
; GitHub: https://github.com/SevenKeyboard/point-get-which-monitor
; Author: SevenKeyboard Ltd. (2025)
; License: The Unlicense
;==============================================================
class VersionManager_pointGetWhichMonitor
{
    static _ := this._init()
    static _init()    {
        global
        POINTGETWHICHMONITOR_VERSION := "1.0.0"
        if (!this._verCheck(&MONITOREXGETUTILS_VERSION, "1.0.0"))
            throw error("MonitorExGetUtils version 1.x is required (minimum 1.0.0).")
        return true
    }
    static _verCheck(&actual, required)    {
        if !isSet(actual)
            return false
        actualMajor     := strSplit(actual, ".",, 2)[1]
        requiredMajor   := strSplit(required, ".",, 2)[1]
        if (actualMajor !== requiredMajor)
            return false
        return verCompare(actual, ">=" required)
    }
}
pointGetWhichMonitor(x?, y?, dwFlags?)    {
    static MONITOR_DEFAULTTONULL:=0x00000000
        ,MONITOR_DEFAULTTOPRIMARY:=0x00000001
        ,MONITOR_DEFAULTTONEAREST:=0x00000002
    if (!isSet(x) || !isSet(y))    {
        point := buffer(8, 0)
        if (!dllCall("User32.dll\GetCursorPos", "Ptr",point.Ptr))
            return 0
        x:=numGet(point,0,"Int"), y:=numGet(point,4,"Int")
    }
    if (isSet(dwFlags))    {
        dwFlags:=(dwFlags==MONITOR_DEFAULTTONULL || dwFlags==MONITOR_DEFAULTTOPRIMARY || dwFlags==MONITOR_DEFAULTTONEAREST)?dwFlags
                :(dwFlags~="iD)^MONITOR_DEFAULTTO(NULL|PRIMARY|NEAREST)$")?%dwFlags%
                :(dwFlags~="iD)^(NULL|PRIMARY|NEAREST)$")?MONITOR_DEFAULTTO%dwFlags%
                :dwFlags
    }  else  {
        dwFlags:=MONITOR_DEFAULTTONULL
    }
    hMonitor:=dllCall("User32.dll\MonitorFromPoint", "Int64",(x&0xFFFFFFFF)|(y<<32), "UInt",dwFlags, "Ptr")
    ret:=0
    for i,info in monitorExGetInfoList()    {
        if (hMonitor==info.hMonitor)    {
            ret:=i
            break
        }
    }
    return ret
}