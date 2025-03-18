import dom from "./dom.mjs";

const TEMPLATE_REGEX = /{([^{}]+?)}/g;

export default class {
	constructor(contents) {
		this.contents = contents;
	};
	7
	apply(obj, processor) {

		//Keys to not escape
		const allowHTMLKeys = new Set(["icon"]); //Example with more: Set(["icon", "description", "content"]);
	
		return this.contents.replace(TEMPLATE_REGEX, (full, match) => {
			const value = match.split(".").reduce((o, property) => o[property], obj);

			
			console.log(match)
			console.log(value)
	
			if (processor) {
				const updated = processor(match, value);
				return typeof updated === "undefined" ? (allowHTMLKeys.has(match) ? value : dom.escapeHTML(value)) : updated;
			}

	
			return allowHTMLKeys.has(match) ? value : dom.escapeHTML(value);
		});
	}
	
}
