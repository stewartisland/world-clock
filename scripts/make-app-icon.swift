// Draws the iPhone app icon (a clock face over a globe) in its light, dark and tinted variants.
// Colours are the ones in Themes/Light.xaml and Themes/Dark.xaml.
//
//   swift scripts/make-app-icon.swift ios/WorldClock/Assets.xcassets/AppIcon.appiconset

import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

struct Palette {
    var backgroundTop: UInt32
    var backgroundBottom: UInt32
    var face: UInt32
    var grid: UInt32
    var hands: UInt32
    var centre: UInt32
}

let size = 1024.0
let variants: [(file: String, palette: Palette)] = [
    ("AppIcon.png", Palette(backgroundTop: 0x5B8DEF, backgroundBottom: 0x2F64D8, face: 0xFFFFFF, grid: 0xDDE6FA, hands: 0x15171C, centre: 0x2F64D8)),
    ("AppIcon-Dark.png", Palette(backgroundTop: 0x1F232B, backgroundBottom: 0x15171C, face: 0x262B34, grid: 0x3A4150, hands: 0xFFFFFF, centre: 0x5B8DEF)),
    ("AppIcon-Tinted.png", Palette(backgroundTop: 0x000000, backgroundBottom: 0x000000, face: 0xE6E6E6, grid: 0xBDBDBD, hands: 0x1A1A1A, centre: 0x1A1A1A)),
]

func color(_ hex: UInt32) -> CGColor {
    CGColor(srgbRed: CGFloat((hex >> 16) & 0xFF) / 255, green: CGFloat((hex >> 8) & 0xFF) / 255, blue: CGFloat(hex & 0xFF) / 255, alpha: 1)
}

func draw(_ p: Palette) -> CGImage {
    let ctx = CGContext(data: nil, width: Int(size), height: Int(size), bitsPerComponent: 8, bytesPerRow: 0,
                        space: CGColorSpace(name: CGColorSpace.sRGB)!, bitmapInfo: CGImageAlphaInfo.noneSkipLast.rawValue)!
    let centre = CGPoint(x: size / 2, y: size / 2)

    // Background (CoreGraphics' origin is bottom-left, so "top" is at y = size).
    let gradient = CGGradient(colorsSpace: nil, colors: [color(p.backgroundBottom), color(p.backgroundTop)] as CFArray, locations: [0, 1])!
    ctx.drawLinearGradient(gradient, start: CGPoint(x: 0, y: 0), end: CGPoint(x: 0, y: size), options: [])

    // Face
    let radius = 340.0
    let face = CGRect(x: centre.x - radius, y: centre.y - radius, width: radius * 2, height: radius * 2)
    ctx.setFillColor(color(p.face))
    ctx.fillEllipse(in: face)

    // Globe lines: meridians and parallels, clipped to the face.
    ctx.saveGState()
    ctx.addEllipse(in: face)
    ctx.clip()
    ctx.setStrokeColor(color(p.grid))
    ctx.setLineWidth(14)
    for fraction in [0.0, 0.5, 0.85] {
        let w = radius * 2 * fraction
        ctx.strokeEllipse(in: CGRect(x: centre.x - w / 2, y: face.minY, width: max(w, 0.1), height: radius * 2))
    }
    for offset in [-0.5, 0.0, 0.5] {
        let y = centre.y + radius * offset
        ctx.move(to: CGPoint(x: face.minX, y: y))
        ctx.addLine(to: CGPoint(x: face.maxX, y: y))
    }
    ctx.strokePath()
    ctx.restoreGState()

    // Hands at 10:10. Angles are clockwise from 12.
    func hand(angle degrees: Double, length: Double, width: Double) {
        let a = degrees * .pi / 180
        ctx.setLineWidth(width)
        ctx.setLineCap(.round)
        ctx.move(to: centre)
        ctx.addLine(to: CGPoint(x: centre.x + sin(a) * length, y: centre.y + cos(a) * length))
        ctx.strokePath()
    }
    ctx.setStrokeColor(color(p.hands))
    hand(angle: 305, length: 180, width: 44)
    hand(angle: 60, length: 260, width: 32)

    ctx.setFillColor(color(p.centre))
    ctx.fillEllipse(in: CGRect(x: centre.x - 30, y: centre.y - 30, width: 60, height: 60))

    return ctx.makeImage()!
}

let folder = URL(fileURLWithPath: CommandLine.arguments.dropFirst().first ?? ".")
for (file, palette) in variants {
    let url = folder.appendingPathComponent(file)
    let dest = CGImageDestinationCreateWithURL(url as CFURL, UTType.png.identifier as CFString, 1, nil)!
    CGImageDestinationAddImage(dest, draw(palette), nil)
    guard CGImageDestinationFinalize(dest) else { fatalError("Couldn't write \(url.path)") }
    print("Wrote \(url.path)")
}
