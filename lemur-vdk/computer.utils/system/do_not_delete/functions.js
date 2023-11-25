// terminal -------------------------
function print(obj) {
    term.print(obj);
}
function alias(cmd, path) {
    term.alias(cmd, path)
}
function call(command) {
    term.call(command);
}
function sleep(ms) {
    return term.sleep(ms);
}
function read() {
    return term.read();
}
function clamp(min, max, value) {
    return Math.min(max, Math.max(min, value))
}
function to_color(color) {
    var packedColor = (color[0] << 24) | (color[1] << 16) | (color[2] << 8) | color[3];
    return packedColor;
}
function create_square() {
    const v1 = new Point(-0.5, -0.5, Color.WHITE)
    const v2 = new Point(-0.5, 0.5, Color.WHITE)
    const v3 = new Point(0.5, 0.5, Color.WHITE)
    const v4 = new Point(0.5, -0.5, Color.WHITE)
    const verts = [v1, v2, v3, v4];
    return verts;
}
// -------------------------



// general -------------------------
function random(max = 1) {
    return interop.random(max);
}
function describe(obj) {
    for (const property in obj) {
        print(property + ": " + obj[property]);
    }
}
//-------------------------



// require -------------------------
let IS_EXPORTING = false;
let __INCLUDES = [];

function require(path) {
    IS_EXPORTING = true;

    if (__INCLUDES.includes(path)) {
        return __INCLUDES[path];
    } else {
        const fn = new Function(file.read(path));
        const result = fn();
        return result;
    }
}
// -------------------------