function print(obj){
    interop.print(obj);
}
function alias(cmd, path){
    interop.alias(cmd, path)
}
function call(command) {
    interop.call(command);
}
class OS {
    id = 0;

    constructor() {
        this.app_classes = [];
    }

    exit(code) {
        interop.exit(code);
    }

    computerID() {
        return this.id;
    }
}
let os = new OS()