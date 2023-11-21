
class paint {
    //#region 
    clean(color) {
        this.width = this.newWidth;
        this.frameData = [];
        for (let y = 0; y < this.width; y++) {
            for (let x = 0; x < this.width; x++) {
                this.frameData.push(color[0])
                this.frameData.push(color[1])
                this.frameData.push(color[2])
                this.frameData.push(color[3])
            }
        }
    }
    setWidth(width) {
        this.newWidth = width;
        this.resizing = true;
    }
    writePixel(x, y, color) {
        let index = (y * this.width + x) * this.bytesPerPixel;
        color.forEach(byte => {
            this.frameData[index] = byte;
            index++;
        });
        this.isDirty = true;
    }
    _render() {
        
        if (this.resizing) {
            this.clean(palette[Color.BLACK]);
            this.resizing = false;
        }
        if (this.isDirty === true)
        {
            app.pushEvent(this.id, 'renderTarget', 'draw_pixels', this.frameData);
            this.frameCt++;
        }
    }
    lerpColors(a, b, t) {
        const result = new Uint8Array(4);
        for (let i = 0; i < 4; i++) {
            result[i] = Math.floor(b[i] * t + a[i] * (1 - t));
        }
        return result;
    }
    changeBrush(left, right){
        
        this.brushColorIndex ++;

        if (this.brushColorIndex >= palette.length){
            this.brushColorIndex= 0;
        }

        this.displayColorName();

    }
    onNetworkEvent(ch,reply,data){
    	const json = JSON.parse(data);
        
        if (json === null || json === undefined){
            print('network packet was null');
            print(data);
            return;
        }

        if (json.data === null || json.data === undefined){
            print('network packet data was null');
            print(data);
        }

    	const index = json.data.colorIndex;
    	const X = json.data.X;
		const Y = json.data.Y;
		
    	if (index != undefined && index > 0 && X != undefined && Y != undefined){
    		this.writePixel(X,Y, palette[index]);
    	}
    }
    displayColorName() {
        const colorName = Object.keys(Color).find(key => Color[key] == this.brushColorIndex);
        app.setProperty(this.id, 'colorNameLabel', 'Content', colorName);
    }
    onMouseMoved(X, Y){

        this.mouseState.x = X;
        this.mouseState.y = Y;

        const width = app.getProperty(this.id, 'renderTarget', 'ActualWidth')
        const height = app.getProperty(this.id, 'renderTarget', 'ActualHeight')

        if (this.mouseState.right === true)
        {
            const X = Math.floor(this.mouseState.x / width * this.width);
            const Y = Math.floor(this.mouseState.y / height * this.width);
            const color = palette[this.brushColorIndex]
            if (network?.IsConnected === true){
                const colorIndex = this.brushColorIndex;
                
                const packet = {
                    X : X,
                    Y : Y,
                    colorIndex :colorIndex,
                };
                const json = JSON.stringify(packet);
                const ch = app.getProperty(this.id, 'chTxt', 'Content');
                const reply = app.getProperty(this.id, 'replyTxt', 'Content');
            	network.send(ch, reply, json);
                print(`sending color data to ${ch} ${reply} ${json}`)
            }
            this.writePixel(X, Y, color)
            this.isDirty = true;
        }
    }
    onMouseDown(left, right){
        this.mouseState.right = right;
        this.mouseState.left = left;
    }
    setupUIEvents() {
        app.eventHandler(this.id, 'this', '_render', XAML_EVENTS.RENDER);
        app.eventHandler(this.id, 'this', '_physics', XAML_EVENTS.RENDER);
        network.eventHandler(this.id, 'onNetworkEvent');
        // brush color button click
        app.eventHandler(this.id, 'changeColorBtn', 'changeBrush', XAML_EVENTS.MOUSE_DOWN);
        app.eventHandler(this.id, 'saveBtn', 'onConnect', XAML_EVENTS.MOUSE_DOWN);

        // save/load image UI
        app.eventHandler(this.id, 'saveBtn', 'onSave', XAML_EVENTS.MOUSE_DOWN);
        app.eventHandler(this.id, 'loadBtn', 'onLoad', XAML_EVENTS.MOUSE_DOWN);

        // image mouse down/up in same method.
        app.eventHandler(this.id, 'this', 'onMouseDown', XAML_EVENTS.MOUSE_DOWN);
        app.eventHandler(this.id, 'this', 'onMouseDown', XAML_EVENTS.MOUSE_UP);

        // image mouse move
        app.eventHandler(this.id, 'this', 'onMouseMoved', XAML_EVENTS.MOUSE_MOVE);
    }
    getIndexedColorData(){
        const data = [0,0,0,0,0,0,0];
        for (let i = 0; i < this.frameData.length; i += 4) {
            const color = [
                this.frameData[i],
                this.frameData[i + 1],
                this.frameData[i + 2],
                this.frameData[i + 3]
            ];
        
            let index = -1;
        
            for (let j = 0; j < palette.length; j++) {
                if (
                    palette[j][0] === color[0] &&
                    palette[j][1] === color[1] &&
                    palette[j][2] === color[2] &&
                    palette[j][3] === color[3]
                ) {
                    index = j;
                    break;
                }
            }
        
            if (index === -1) {
                print('Color not found: ' + `${color}`);
                return;
            }
        
            data.push(index);
        }
        return data;
    }
    readIndexedColorData(data){
        const result = [];
        const input = JSON.parse(data);

        for (let i = 0; i < input.length; ++i){
            result[i + 0] = palette[input[i + 0]]
            result[i + 1] = palette[input[i + 1]]
            result[i + 2] = palette[input[i + 2]]
            result[i + 3] = palette[input[i + 3]]
        }
        print(result.length);
        return result;
    }
    onSave(){

        const path = app.getProperty(this.id, 'nameBox', 'Text')
        const data = this.getIndexedColorData();

        if (path !== undefined && typeof path === 'string' && data.length !== 0){
            file.write(path, JSON.stringify(data));
            print(`saved ${path}!`)
        }
        else{
            print(`failed to save : ${path} :: ${data}`);
        }
    }
    onLoad(){

        const path = app.getProperty(this.id, 'nameBox', 'Text')

        print(path);

        if (path === "" || path === undefined){
            print("you must provide a path to load from");
            return;
        }

        const json = file.read(path);

        const data = this.readIndexedColorData(json);
        
        if (data.length === 0){
            print('the file was read, but no data was found');
            return;
        }

        this.setWidth(Math.sqrt(data.length));
        this.frameData = data;
        this.isDirty = true;
    }
    //#endregion
    constructor(id) {
        // for the engine.
        this.id = id;

        // object representing mouse state
        this.mouseState = 
        {
            x: 0,
            y : 0,
            left : false,
            right : false
        }
        
        this.brushColorIndex = 0;
        
        this.bytesPerPixel = 4;
        this.width = 38;

        this.frameData = [];
        this.isDirty = true;
        
        this.resizing = false;
        this.newWidth = this.width;

        this.setupUIEvents();
        
        this.clean(palette[14]);

        this.displayColorName();
    }
}