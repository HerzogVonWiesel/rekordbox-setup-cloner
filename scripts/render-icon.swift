// Standalone artwork generator. Does not launch the app or access rekordbox settings.
import Foundation
import CoreGraphics
import CoreText
import ImageIO
import UniformTypeIdentifiers

enum IconStyle: String, CaseIterable {
    case record, sync
}

let resources = URL(fileURLWithPath: CommandLine.arguments[1], isDirectory: true)
let build = URL(fileURLWithPath: CommandLine.arguments[2], isDirectory: true)
guard let selectedStyle = IconStyle(rawValue: CommandLine.arguments.dropFirst(3).first ?? "sync") else {
    fatalError("Icon style must be record or sync.")
}

let canvas: CGFloat = 1024
let tile = CGRect(x: 88, y: 88, width: 848, height: 848)
let rearCenter = CGPoint(x: 438, y: 422)
let frontCenter = CGPoint(x: 580, y: 578)
let radius: CGFloat = 222
let aperture: CGFloat = 137
let seamWidth: CGFloat = 44
let seamAngles: [CGFloat] = [-150, -30, 90]
let mint: UInt32 = 0x70E0D9
let white: UInt32 = 0xF3F6FA
let rearFill: UInt32 = 0x18343A
let frontFill: UInt32 = 0x141B25

func color(_ hex: UInt32, alpha: CGFloat = 1) -> CGColor {
    CGColor(red: CGFloat((hex >> 16) & 255) / 255,
            green: CGFloat((hex >> 8) & 255) / 255,
            blue: CGFloat(hex & 255) / 255, alpha: alpha)
}

func radial(_ center: CGPoint, radius: CGFloat, degrees: CGFloat) -> CGPoint {
    let angle = degrees * .pi / 180
    return CGPoint(x: center.x + radius * cos(angle), y: center.y + radius * sin(angle))
}

func roundedHexagon(_ center: CGPoint) -> CGPath {
    let vertices = (0..<6).map { radial(center, radius: radius, degrees: CGFloat($0) * 60 - 90) }
    let path = CGMutablePath()
    for index in vertices.indices {
        let vertex = vertices[index]
        let previous = vertices[(index + 5) % 6]
        let next = vertices[(index + 1) % 6]
        let start = CGPoint(x: vertex.x + (previous.x - vertex.x) * 0.055,
                            y: vertex.y + (previous.y - vertex.y) * 0.055)
        let end = CGPoint(x: vertex.x + (next.x - vertex.x) * 0.055,
                          y: vertex.y + (next.y - vertex.y) * 0.055)
        if index == 0 { path.move(to: start) } else { path.addLine(to: start) }
        path.addQuadCurve(to: end, control: vertex)
    }
    path.closeSubpath()
    return path
}

func seams(_ center: CGPoint) -> CGPath {
    let path = CGMutablePath()
    for angle in seamAngles {
        path.move(to: center)
        path.addLine(to: radial(center, radius: radius + 40, degrees: angle))
    }
    return path
}

struct Arrow {
    let arc: CGPath
    let head: CGPath
}

func arrow(start: CGFloat, end: CGFloat) -> Arrow {
    let ringRadius: CGFloat = 76
    let arc = CGMutablePath()
    arc.addArc(center: frontCenter, radius: ringRadius,
               startAngle: start * .pi / 180, endAngle: end * .pi / 180, clockwise: false)
    let endpoint = radial(frontCenter, radius: ringRadius, degrees: end)
    let angle = end * .pi / 180
    let tangent = CGPoint(x: -sin(angle), y: cos(angle))
    let normal = CGPoint(x: cos(angle), y: sin(angle))
    let tip = CGPoint(x: endpoint.x + tangent.x * 23, y: endpoint.y + tangent.y * 23)
    let left = CGPoint(x: endpoint.x - tangent.x * 12 + normal.x * 22,
                       y: endpoint.y - tangent.y * 12 + normal.y * 22)
    let right = CGPoint(x: endpoint.x - tangent.x * 12 - normal.x * 22,
                        y: endpoint.y - tangent.y * 12 - normal.y * 22)
    let head = CGMutablePath()
    head.addLines(between: [tip, left, right])
    head.closeSubpath()
    return Arrow(arc: arc, head: head)
}

let arrows = [arrow(start: 200, end: 340), arrow(start: 20, end: 160)]
let background = CGPath(roundedRect: tile, cornerWidth: 190, cornerHeight: 190, transform: nil)

func bitmap(width: Int, height: Int) throws -> CGContext {
    guard let space = CGColorSpace(name: CGColorSpace.sRGB),
          let context = CGContext(data: nil, width: width, height: height, bitsPerComponent: 8,
                                  bytesPerRow: width * 4, space: space,
                                  bitmapInfo: CGImageAlphaInfo.premultipliedLast.rawValue) else {
        throw NSError(domain: "Artwork", code: 1, userInfo: [NSLocalizedDescriptionKey: "Could not create bitmap."])
    }
    context.translateBy(x: 0, y: CGFloat(height))
    context.scaleBy(x: 1, y: -1)
    context.setAllowsAntialiasing(true)
    return context
}

func writePNG(_ context: CGContext, to destination: URL) throws {
    guard let image = context.makeImage(),
          let writer = CGImageDestinationCreateWithURL(destination as CFURL, UTType.png.identifier as CFString, 1, nil) else {
        throw NSError(domain: "Artwork", code: 2, userInfo: [NSLocalizedDescriptionKey: "Could not write bitmap."])
    }
    CGImageDestinationAddImage(writer, image, nil)
    guard CGImageDestinationFinalize(writer) else {
        throw NSError(domain: "Artwork", code: 3, userInfo: [NSLocalizedDescriptionKey: "Could not finish bitmap."])
    }
}

func drawMark(_ context: CGContext, center: CGPoint, ink: UInt32, fill: UInt32) {
    let outline = roundedHexagon(center)
    context.addPath(outline)
    context.setFillColor(color(fill))
    context.fillPath()

    // The supplied mark uses three solid hexagonal sectors around an open circular centre.
    let ring = CGMutablePath()
    ring.addPath(outline)
    ring.addEllipse(in: CGRect(x: center.x - aperture, y: center.y - aperture, width: aperture * 2, height: aperture * 2))
    context.addPath(ring)
    context.setFillColor(color(ink))
    context.drawPath(using: .eoFill)

    context.saveGState()
    context.addPath(outline)
    context.clip()
    context.addPath(seams(center))
    context.setStrokeColor(color(fill))
    context.setLineWidth(seamWidth)
    context.setLineCap(.butt)
    context.strokePath()
    context.restoreGState()
}

func render(size: Int, style: IconStyle, destination: URL) throws {
    let context = try bitmap(width: size, height: size)
    context.scaleBy(x: CGFloat(size) / canvas, y: CGFloat(size) / canvas)

    context.saveGState()
    context.addPath(background)
    context.clip()
    let gradient = CGGradient(colorsSpace: CGColorSpace(name: CGColorSpace.sRGB),
                              colors: [color(0x292F3A), color(0x0C1017)] as CFArray,
                              locations: [0, 1])!
    context.drawLinearGradient(gradient, start: CGPoint(x: 256, y: 88), end: CGPoint(x: 768, y: 936),
                               options: [.drawsBeforeStartLocation, .drawsAfterEndLocation])
    context.restoreGState()
    context.addPath(background)
    context.setStrokeColor(color(0xFFFFFF, alpha: 0.12))
    context.setLineWidth(2)
    context.strokePath()

    context.saveGState()
    if style == .sync {
        context.translateBy(x: 512, y: 512)
        context.scaleBy(x: 1.3, y: 1.3)
        context.translateBy(x: -frontCenter.x, y: -frontCenter.y)
    } else {
        drawMark(context, center: rearCenter, ink: mint, fill: rearFill)
    }
    drawMark(context, center: frontCenter, ink: white, fill: frontFill)
    context.setFillColor(color(mint))
    if style == .record {
        context.fillEllipse(in: CGRect(x: frontCenter.x - 57, y: frontCenter.y - 57, width: 114, height: 114))
    } else {
        context.setStrokeColor(color(mint))
        context.setLineWidth(19)
        context.setLineCap(.round)
        for arrow in arrows {
            context.addPath(arrow.arc)
            context.strokePath()
            context.addPath(arrow.head)
            context.fillPath()
        }
    }
    context.restoreGState()
    try writePNG(context, to: destination)
}

// Export the actual Core Graphics paths so the SVG and PNG share one source of geometry.
func svgPath(_ path: CGPath) -> String {
    var commands: [String] = []
    func point(_ point: CGPoint) -> String { String(format: "%.3f %.3f", Double(point.x), Double(point.y)) }
    path.applyWithBlock { pointer in
        let element = pointer.pointee
        switch element.type {
        case .moveToPoint: commands.append("M \(point(element.points[0]))")
        case .addLineToPoint: commands.append("L \(point(element.points[0]))")
        case .addQuadCurveToPoint: commands.append("Q \(point(element.points[0])) \(point(element.points[1]))")
        case .addCurveToPoint: commands.append("C \(point(element.points[0])) \(point(element.points[1])) \(point(element.points[2]))")
        case .closeSubpath: commands.append("Z")
        @unknown default: break
        }
    }
    return commands.joined(separator: " ")
}

func svg(style: IconStyle) -> String {
    func mark(center: CGPoint, name: String, ink: String, fill: String) -> String {
        let shape = svgPath(roundedHexagon(center))
        return """
          <defs><clipPath id="\(name)-clip"><path d="\(shape)"/></clipPath></defs>
          <path d="\(shape)" fill="\(fill)"/>
          <g clip-path="url(#\(name)-clip)">
            <path d="\(shape)" fill="\(ink)"/>
            <circle cx="\(center.x)" cy="\(center.y)" r="\(aperture)" fill="\(fill)"/>
            <path d="\(svgPath(seams(center)))" fill="none" stroke="\(fill)" stroke-width="\(seamWidth)"/>
          </g>
        """
    }
    let centre: String
    if style == .record {
        centre = "<circle cx=\"580\" cy=\"578\" r=\"57\" fill=\"#70e0d9\"/>"
    } else {
        centre = arrows.map { arrow in
            "<path d=\"\(svgPath(arrow.arc))\" fill=\"none\" stroke=\"#70e0d9\" stroke-width=\"19\" stroke-linecap=\"round\"/>\n" +
            "<path d=\"\(svgPath(arrow.head))\" fill=\"#70e0d9\"/>"
        }.joined(separator: "\n")
    }
    let motif = style == .record ? "a centre dot" : "two sync arrows"
    let description = style == .record ? "Overlapping three-part hexagons, mint behind white" : "One centred white three-part hexagon"
    let frontMark = mark(center: frontCenter, name: "front", ink: "#f3f6fa", fill: "#141b25")
    let symbol: String
    if style == .sync {
        symbol = "<g transform=\"translate(512 512) scale(1.3) translate(-580 -578)\">\n\(frontMark)\n\(centre)\n</g>"
    } else {
        symbol = mark(center: rearCenter, name: "rear", ink: "#70e0d9", fill: "#18343a") + "\n" + frontMark + "\n" + centre
    }
    return """
    <svg xmlns="http://www.w3.org/2000/svg" width="1024" height="1024" viewBox="0 0 1024 1024" role="img" aria-labelledby="title description">
      <title id="title">Rekordbox Setup Cloner — \(style.rawValue)</title>
      <desc id="description">\(description), with \(motif) on a charcoal rounded square.</desc>
      <defs>
        <linearGradient id="tile" gradientUnits="userSpaceOnUse" x1="256" y1="88" x2="768" y2="936">
          <stop stop-color="#292f3a"/><stop offset="1" stop-color="#0c1017"/>
        </linearGradient>
      </defs>
      <rect x="88" y="88" width="848" height="848" rx="190" fill="url(#tile)"/>
      <rect x="88" y="88" width="848" height="848" rx="190" fill="none" stroke="#fff" stroke-opacity=".12" stroke-width="2"/>
      \(symbol)
    </svg>
    """
}

func comparison() throws {
    let context = try bitmap(width: 1600, height: 1080)
    context.setFillColor(color(0xEEF1F4))
    context.fill(CGRect(x: 0, y: 0, width: 1600, height: 1080))

    func text(_ string: String, x: CGFloat, y: CGFloat, size: CGFloat, bold: Bool = false, ink: UInt32 = 0x19212D) {
        let font = CTFontCreateWithName((bold ? "HelveticaNeue-Bold" : "HelveticaNeue") as CFString, size, nil)
        let attributed = NSAttributedString(string: string, attributes: [
            NSAttributedString.Key(kCTFontAttributeName as String): font,
            NSAttributedString.Key(kCTForegroundColorAttributeName as String): color(ink)
        ])
        context.saveGState()
        context.translateBy(x: x, y: y)
        context.scaleBy(x: 1, y: -1)
        context.textMatrix = .identity
        CTLineDraw(CTLineCreateWithAttributedString(attributed), context)
        context.restoreGState()
    }

    text("REKORDBOX SETUP CLONER", x: 90, y: 87, size: 20, bold: true, ink: 0x5B6878)
    text("The final direction: sync.", x: 90, y: 148, size: 44, bold: true)
    for (index, style) in IconStyle.allCases.enumerated() {
        let x: CGFloat = index == 0 ? 100 : 860
        let imageURL = resources.appendingPathComponent("AppIcon-\(style.rawValue).png")
        guard let source = CGImageSourceCreateWithURL(imageURL as CFURL, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            throw NSError(domain: "Artwork", code: 4, userInfo: [NSLocalizedDescriptionKey: "Could not compose icon comparison."])
        }
        context.saveGState()
        context.translateBy(x: x, y: 850)
        context.scaleBy(x: 1, y: -1)
        context.draw(image, in: CGRect(x: 0, y: 0, width: 640, height: 640))
        context.restoreGState()
        text(index == 0 ? "RECORD  /  EARLIER CONCEPT" : "SYNC  /  SELECTED ICON", x: x + 55, y: 909, size: 28, bold: true)
        text(index == 0 ? "Overlapping hexagons." : "One mark. Your setup, everywhere.",
             x: x + 55, y: 949, size: 22, ink: 0x5B6878)
    }
    text("Charcoal, white and mint", x: 90, y: 1030, size: 18, ink: 0x5B6878)
    try writePNG(context, to: resources.appendingPathComponent("Icon-Comparison.png"))
}

for style in IconStyle.allCases {
    let iconset = build.appendingPathComponent("AppIcon-\(style.rawValue).iconset", isDirectory: true)
    try FileManager.default.createDirectory(at: iconset, withIntermediateDirectories: true)
    for pointSize in [16, 32, 128, 256, 512] {
        for scale in [1, 2] {
            let suffix = scale == 2 ? "@2x" : ""
            let filename = "icon_\(pointSize)x\(pointSize)\(suffix).png"
            try render(size: pointSize * scale, style: style, destination: iconset.appendingPathComponent(filename))
        }
    }
    try render(size: 1024, style: style, destination: resources.appendingPathComponent("AppIcon-\(style.rawValue).png"))
    try svg(style: style).write(to: resources.appendingPathComponent("AppIcon-\(style.rawValue).svg"), atomically: true, encoding: .utf8)
}
try render(size: 1024, style: selectedStyle, destination: resources.appendingPathComponent("AppIcon.png"))
try svg(style: selectedStyle).write(to: resources.appendingPathComponent("AppIcon.svg"), atomically: true, encoding: .utf8)
try comparison()
