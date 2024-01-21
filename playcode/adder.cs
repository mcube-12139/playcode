class AdderParam {
    public int a { get; set; }
    public int b { get; set; }
}

class AdderResult : BaseResult {
    public int sum { get; set; }
}

class Adder {
    static public AdderResult response(AdderParam param) {
        return new AdderResult() {
            sum = param.a + param.b
        };
    }
}
