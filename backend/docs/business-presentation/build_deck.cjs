// Generate an editable presentation. Dependency: pptxgenjs.
const pptxgen = require('pptxgenjs');
const fs = require('node:fs');
const path = require('node:path');
const pptx = new pptxgen();
pptx.layout = 'LAYOUT_WIDE';
pptx.author = 'SwiftReview project team';
pptx.subject = 'Business introduction for leadership and future users';
pptx.title = 'ORP — SWIFT message review';
pptx.company = 'Operations Reporting and Processing';
pptx.lang = 'en-GB';
pptx.theme = { headFontFace: 'Arial', bodyFontFace: 'Arial', lang: 'en-GB' };
const C = {green:'004831',dark:'003322',lime:'C4D600',mint:'EAF2EE',paper:'F6F8F6',ink:'1F2522',muted:'68716C',white:'FFFFFF',line:'D8DDD9',teal:'317589',red:'C3272B',redpale:'FBEDEE'};
const S = pptx.ShapeType;
const notes=[];
let current;
function shape(type,x,y,w,h,fill,line=fill,more={}) {current.addShape(type,{x,y,w,h,fill:{color:fill},line:{color:line,width:1},...more});}
function box(x,y,w,h,fill=C.white,line=fill){shape(S.roundRect,x,y,w,h,fill,line,{radius:0.12,rectRadius:0.12});}
function txt(text,x,y,w,h,size=18,color=C.ink,bold=false,extra={}) {current.addText(text,{x,y,w,h,fontFace:'Arial',fontSize:size,color,bold,margin:0,breakLine:false,valign:'top',paraSpaceAfter:0,fit:'resize',...extra});}
function line(x1,y1,x2,y2,color=C.green,width=1.6,arrow=false,dash=false){current.addShape(S.line,{x:x1,y:y1,w:x2-x1,h:y2-y1,line:{color,width,beginArrowType:'none',endArrowType:arrow?'triangle':'none',dashType:dash?'dash':'solid'}});}
function pill(label,x,y,w,fill=C.mint,color=C.green){box(x,y,w,.35,fill);txt(label,x+.13,y+.072,w-.26,.2,10.5,color,true);}
function circle(label,x,y,d=.52,fill=C.green,color=C.white){shape(S.ellipse,x,y,d,d,fill);txt(label,x,y+(d<.4?.06:.11),d,d<.4?.18:.3,d<.4?10:17,color,true,{align:'center'});}
function icon(kind,x,y,size=.54,color=C.green,bg=C.mint){
  shape(S.ellipse,x,y,size,size,bg);
  const u=size/100, X=n=>x+n*u, Y=n=>y+n*u;
  if(kind==='check'){line(X(27),Y(51),X(43),Y(66),color,2.2);line(X(43),Y(66),X(74),Y(33),color,2.2);}
  else if(kind==='person'){shape(S.ellipse,X(37),Y(23),26*u,26*u,color);shape(S.roundRect,X(26),Y(55),48*u,24*u,color);}
  else if(kind==='document'){shape(S.rect,X(31),Y(23),39*u,54*u,bg,color);line(X(39),Y(39),X(62),Y(39),color,1.3);line(X(39),Y(51),X(62),Y(51),color,1.3);line(X(39),Y(63),X(56),Y(63),color,1.3);}
  else if(kind==='clock'){shape(S.ellipse,X(25),Y(25),50*u,50*u,bg,color);line(X(50),Y(35),X(50),Y(51),color,1.7);line(X(50),Y(51),X(63),Y(59),color,1.7);}
  else if(kind==='grid'){for(let a=0;a<2;a++)for(let b=0;b<2;b++)shape(S.rect,X(28+a*25),Y(28+b*25),18*u,18*u,color);}
  else {shape(S.hexagon,X(22),Y(20),56*u,61*u,bg,color);line(X(36),Y(48),X(47),Y(59),color,1.7);line(X(47),Y(59),X(66),Y(39),color,1.7);}
}
function footer(i,dark){txt('ORP  /  Operations Reporting and Processing',.58,7.06,8,.18,9,dark?'B1CCC4':C.muted);txt(String(i).padStart(2,'0'),12.1,7.02,.62,.25,11,dark?'B1CCC4':C.muted,false,{align:'right'});}
function slide(title,subtitle,section,note,sources='',dark=false){
 current=pptx.addSlide();current.background={color:dark?C.dark:C.paper};
 const i=notes.length+1;
 if(title){txt(section,.6,.47,11,.23,11,dark?C.lime:C.green,true);txt(title,.58,.95,12.1,.68,34,dark?C.white:C.ink,true);if(subtitle)txt(subtitle,.6,1.72,11.95,.55,16,dark?'B1CCC4':C.muted);}
 footer(i,dark);current.addNotes(note+'\n\nEvidence: '+sources);notes.push({i,title,note,sources});return current;
}
function image(name,x,y,w,h){const p=path.join(__dirname,'assets',name),data=fs.readFileSync(p);const iw=data.readUInt32BE(16),ih=data.readUInt32BE(20),scale=Math.min(w/iw,h/ih);current.addImage({path:p,x:x+(w-iw*scale)/2,y:y+(h-ih*scale)/2,w:iw*scale,h:ih*scale});}
function caption(t,x=.6,y=6.56,w=12.1){txt(t,x,y,w,.35,11,C.muted);}
function labelBlock(n,title,body,x,y,w=3.55){circle(n,x,y);txt(title,x+.73,y+.01,w-.73,w<3?.63:.42,w<3?18:20,C.green,true);txt(body,x+.73,y+(w<3?.68:.5),w-.73,.65,15,C.muted);}

// 01 — a visual promise, grounded in actual workflow capabilities.
slide('', '', '',
 'Today I will show how Operations Reporting and Processing, or ORP, supports the review of SWIFT messages. The central idea is simple: each message has a visible stage, an accountable reviewer when assigned, and a record of the decisions made. For leadership, this creates a clearer operating process. For users, it brings the queue and review actions together. We will follow a message through the application, look at the controls behind that journey, and finish with the decisions needed for a pilot. The screens use synthetic demonstration data. This presentation describes the current implementation, rather than a claim of production readiness.',
 'README.md; backend/README.md; backend/docs/MESSAGE_STATE_MACHINE.md',true);
txt('Operations Reporting and Processing',.64,.64,7.6,.35,14,'B1CCC4');
txt('SWIFT reviews.\nClear ownership.',.6,1.62,7.6,1.9,46,C.white,true);
txt('A shared workflow for assigning, reviewing\nand tracing message decisions.',.65,3.87,7.1,.9,23,'D5E4E1');
pill('Leadership + future users',.65,5.6,2.73,C.lime,C.dark);
txt('Product overview  •  September 2026',.66,6.22,6,.33,12,'B1CCC4');
line(9.0,2.0,11.62,3.42,'589284',2);line(11.62,3.42,9.0,5.0,'589284',2);line(9.0,5.0,9.0,2.0,'589284',2);
icon('document',8.45,1.5,1.15,C.dark,C.lime);icon('person',11.0,2.88,1.15,C.dark,'D5E4E1');icon('check',8.45,4.45,1.15,C.dark,C.lime);
txt('Message',8.16,2.85,1.75,.3,15,C.white,true,{align:'center'});txt('Reviewer',10.68,4.18,1.75,.3,15,C.white,true,{align:'center'});txt('Decision',8.16,5.75,1.75,.3,15,C.white,true,{align:'center'});

// 02 — distinct value for the two audiences.
slide('One workflow. Three practical benefits.',
 'A shared view of work connects responsibility, review and follow-up.', 'Business value',
 'There are three practical benefits. First, teams can identify the current stage and the person assigned to a message. Second, reviewers have a personal work queue, so they can find their assigned messages and take the permitted next action. Third, authorised users can follow the history of assignments and decisions. These are capabilities supported by the current application. We should validate the resulting time savings and operating improvements in a pilot; there are no measured productivity or cost-saving figures behind this presentation. The objective is to make the process easier to manage and use, with evidence of what happened when questions arise.',
 'frontend/src/pages/messages/AssignedMessagesPage.tsx; MessagesGrid.tsx; AuditTrailDrawer.tsx');
const vals=[['grid','For leadership','See work clearly','Stage and owner are visible\nin the message queue.'],['person','For reviewers','Focus on your work','Review actions and assigned\nmessages in one place.'],['clock','For control teams','Trace the decision','History links actions to people,\ntimes and workflow changes.']];
vals.forEach((v,i)=>{const x=.6+i*4.13;box(x,2.57,3.88,3.4);icon(v[0],x+.28,2.85,.7);txt(v[1],x+.28,3.83,3.3,.35,13,C.green,true);txt(v[2],x+.28,4.32,3.3,.69,22,C.ink,true);txt(v[3],x+.28,5.15,3.32,.64,15,C.muted);});
caption('Expected operating benefits; their scale will be measured during the pilot.');

// 03 — the flow, including the manual hand-off between levels.
slide('Every message follows a visible path',
 'The workflow records progress from registration to the final review outcome.', 'The message journey',
 'Messages arrive through the configured source integration and are registered against a review workflow. An authorised person then assigns an eligible reviewer. The reviewer reads the message and records a decision. When another review level is required, the message waits for a new manual assignment to a different eligible reviewer. Once every required level is approved, the review is complete. A rejection stops the review workflow and is currently final. In this product, completed means that the configured review has finished; it should not be described as execution or release of a payment. This distinction keeps the business promise aligned with the implemented scope.',
 'backend/docs/MESSAGE_STATE_MACHINE.md; backend/docs/AWH_DATA_INGESTION.md');
const journey=[['01','Receive','Source message'],['02','Register','Apply workflow'],['03','Assign','Choose reviewer'],['04','Review','Record decision'],['05','Complete','All levels approved']];
journey.forEach((v,i)=>{let x=.6+i*2.52;box(x,2.85,2.1,1.83,i===4?C.green:C.white);circle(v[0],x+.2,3.08,.48,i===4?C.lime:C.mint,i===4?C.dark:C.green);txt(v[1],x+.2,3.76,1.73,.34,21,i===4?C.white:C.ink,true);txt(v[2],x+.2,4.25,1.74,.25,12,i===4?'D5E4E1':C.muted);if(i<4)line(x+2.16,3.76,x+2.45,3.76,C.green,1.7,true);});
line(8.33,4.79,8.33,5.39,C.teal);line(8.33,5.39,5.77,5.39,C.teal,1.6,true);txt('Another level → new manual assignment',4.48,5.66,5.0,.35,15,C.teal,true);
pill('Rejection stops the workflow',.6,5.32,3.06,C.redpale,C.red);
caption('“Complete” refers to message review. The application does not demonstrate payment execution.');

// 04 — the real workspace.
slide('A shared queue for the team',
 'Find a message, understand its stage and see who owns the next action.', 'The workspace',
 'This is the current message workspace, using demonstration data. The table brings together message type, organisational context, current stage, received date, amount and assignee. Users can filter the available messages and inspect the actions offered for each row. Visibility and actions depend on their permissions and organisational scope. A personal My work view is available for reviewers, together with a My departments view. The current interface is in English. For a live walkthrough, start with the team queue, point out a waiting message and its assignee, then switch to a reviewer’s personal queue. The numbers visible here are synthetic examples, not business performance figures.',
 'frontend/src/pages/messages/MessagesGrid.tsx; AssignedMessagesPage.tsx; assets/messages.png');
box(.6,2.5,9.07,3.83,C.white,C.line);image('queue-detail.png',.7,2.6,8.87,3.63);
labelBlock('1','Find the message','Filter by the fields\nthat matter to the task.',9.97,2.57,2.7);
labelBlock('2','Read the stage','See progress and\ncurrent ownership.',9.97,4.3,2.7);
caption('Actual interface • Synthetic demonstration data • Access varies by user.');

// 05 — accountability and eligibility.
slide('Ownership is explicit at each review stage',
 'An authorised assigner chooses from eligible reviewers; the system checks the rules.', 'Assignment and access',
 'Assignment is a deliberate business action, not automatic workload distribution. An authorised assigner chooses a reviewer from the candidates the application permits for this message. Eligibility combines organisational access, permission for the required review level, and independence from earlier approvals on the same message. Users cannot assign a message to themselves. Responsibility can be transferred before a review starts, while reassignment during an active review is blocked. Approval clears the assignment; if another stage is required, an authorised person assigns the next reviewer. These controls make ownership explicit, but staffing cover and the person responsible for monitoring unassigned work still need to be agreed for the pilot.',
 'docs/BUSINESS_WORKFLOW_DECISIONS.md; backend/docs/MESSAGE_STATE_MACHINE.md; AssignmentPopup.tsx');
const gates=[['person','Organisational access','Relevant branch and department'],['shield','Review permission','Authorised for the required level'],['check','Independent reviewer','No earlier approval on this message']];
gates.forEach((g,i)=>{let y=2.55+i*1.18;icon(g[0],.66,y,.65);txt(g[1],1.58,y+.02,5.3,.35,20,C.green,true);txt(g[2],1.58,y+.52,5.3,.4,15,C.muted);});
box(7.28,2.55,5.45,3.66,C.white,C.line);image('assignment-dialog.png',7.48,2.76,5.04,3.22);
caption('Manual assignment • No self-assignment • Reassignment is available before review starts.');

// 06 — variable depth without a state-machine dump.
slide('Match review depth to the process',
 'Configured workflows support one, two or three required review levels.', 'Review controls',
 'Not every message needs the same review depth. ORP supports workflows with one, two or three required levels. Each approval moves the message to the next required level, or completes the review if no level remains. In a multi-level workflow, a user who approved an earlier level cannot perform a later one on the same message. This is the four-eyes principle in practical terms: later approval requires another eligible person. These are configured workflows; this presentation does not claim a business-user workflow designer exists. The specific message types and conditions that allow optional levels to be skipped remain a business decision to confirm before pilot configuration.',
 'backend/src/ORP.Domain/Workflows/WorkflowDefinition.cs; backend/docs/MESSAGE_STATE_MACHINE.md; docs/BUSINESS_WORKFLOW_DECISIONS.md');
for(let row=0;row<3;row++){let y=2.7+row*1.08;txt(['One level','Two levels','Three levels'][row],.67,y+.12,2.2,.4,20,C.ink,true);for(let i=0;i<=row;i++){let x=3.21+i*2.05;box(x,y,1.65,.73,C.white,C.line);txt('Review '+(i+1),x+.18,y+.2,1.3,.28,16,C.green,true);line(x+1.70,y+.36,x+1.96,y+.36,C.green,1.6,true);}let end=3.21+(row+1)*2.05;icon('check',end,y+.02,.67,C.dark,C.lime);}
box(10.54,2.65,2.2,3.3,C.green);txt('Different\nreviewers',10.8,3.11,1.67,.98,23,C.white,true);txt('Each later\nlevel needs\na different\nreviewer.',10.8,4.46,1.64,1.16,16,'D5E4E1');
caption('The pilot must confirm the mapping between message types, departments and required review levels.');

// 07 — the user’s task.
slide('Review the message. Record the decision.',
 'The reviewer works from My work and sees the message content alongside decision actions.', 'The reviewer experience',
 'For the reviewer, the everyday journey is short. Open My work, select a message assigned to you, and choose Review. The review window shows the message content and an optional comment field. An authorised reviewer can approve or reject. The application verifies ownership and the required stage before accepting the action. Approval either completes the review or sends it to the waiting state for the next required level. Rejection is final in the current version, so training should make that consequence clear. An API capability also exists to withdraw the reviewer’s own latest approval under defined conditions; there is no demonstrated Undo button in this screen, so do not promise a user-facing undo journey during the demo.',
 'frontend/src/pages/messages/ReviewDecisionPopup.tsx; reviewDecision.ts; backend/docs/MESSAGE_STATE_MACHINE.md');
box(.6,2.51,7.9,3.82,C.white,C.line);image('review-detail.png',.79,2.67,7.52,3.49);
labelBlock('1','Open My work','Find a message\nassigned to you.',8.9,2.53,3.7);
labelBlock('2','Inspect the content','Read the message and\nadd a comment if needed.',8.9,3.84,3.7);
labelBlock('3','Approve or reject','Record the decision\nfor the current level.',8.9,5.15,3.7);
caption('Actual interface • Synthetic data • Rejection is final in the current implementation.');

// 08 — history paired with a simplified illustrative timeline.
slide('A decision comes with a traceable history',
 'Authorised users can see who acted, when they acted and how the workflow changed.', 'Audit and follow-up',
 'When a question arises, the message’s audit history provides the sequence of recorded actions. It includes registration, assignment, review start, approval or rejection, and related changes in workflow state. Events carry the actor and timestamp, with comments or assignment details when applicable. This supports investigation and operational handover without relying only on someone’s recollection. Access to audit history is permission-controlled. The implementation preserves audit entries through its persistence boundary, but this should not be presented as a separately certified immutable archive. Retention, external audit requirements and operating controls need to be confirmed with the organisation before production use.',
 'frontend/src/pages/messages/AuditTrailDrawer.tsx; backend/docs/MESSAGE_STATE_MACHINE.md; assets/audit-panel.png');
line(1.01,2.97,1.01,5.86,C.line,2.4);
const events=[['Message registered','The workflow begins.'],['Reviewer assigned','Responsibility is recorded.'],['Review started','The active reviewer is identified.'],['Decision recorded','Outcome and context remain visible.']];
events.forEach((e,i)=>{let y=2.64+i*.91;circle(String(i+1),.76,y,.5,i===3?C.green:C.mint,i===3?C.white:C.green);txt(e[0],1.55,y+.01,5.7,.35,20,C.ink,true);txt(e[1],1.55,y+.45,5.7,.32,15,C.muted);});
box(7.84,2.52,4.9,3.91,C.white,C.line);pill('One recorded decision',8.12,2.86,2.48);image('audit-decision.png',8.02,3.39,4.53,2.56);
caption('Illustrative event sequence on the left; actual decision detail with synthetic data on the right.');

// 09 — shared storage architecture; the import does not call the API.
slide('One workspace. A shared review record.',
 'Source integration, business rules and the user interface have clear responsibilities.', 'Architecture at a glance',
 'At a high level, the architecture has five parts. The existing SWIFT source is read by a scheduled integration component. That component registers new messages in the application’s shared database. The application service reads and updates that same store while enforcing workflow and access rules. Users work in the browser. This is a modular application, rather than a fleet of independent microservices. The underlying technologies are React for the interface, .NET for the service, and SQL Server for persistent storage. Repeated imports are designed to avoid registering duplicate source messages and to preserve existing review progress. For deployment, the approved source connector, corporate sign-in, routing configuration and operational arrangements must be completed and verified.',
 'backend/README.md; backend/src/ORP.Sync/README.md; backend/docs/AWH_DATA_INGESTION.md; frontend/package.json');
pill('Scheduled intake',.64,2.61,2.02);pill('Shared application data',5.3,2.61,2.38);pill('Interactive review',10.11,2.61,2.18);
const nodes=[['SWIFT\nsource','Existing environment'],['Scheduled\nintegration','Import + routing'],['Review\ndata store','SQL Server'],['Application\nservice','Workflow + access'],['Browser\nworkspace','React interface']];
nodes.forEach((n,i)=>{let x=.6+i*2.53;box(x,3.38,2.04,1.77,i===2?C.green:C.white);txt(n[0],x+.18,3.66,1.68,.76,20,i===2?C.white:C.ink,true);txt(n[1],x+.18,4.65,1.7,.26,11,i===2?'D5E4E1':C.muted);if(i<4)line(x+2.11,4.22,x+2.43,4.22,C.green,1.8,true);if(i>=2&&i<4)line(x+2.43,4.43,x+2.11,4.43,C.green,1.8,true);});
box(.6,5.64,12.13,.59,C.mint);txt('New messages enter through integration. Reviews and audit events stay linked in one application data store.',.83,5.82,11.64,.27,14,C.green,true);
caption('High-level design for persistent deployment; the screenshots use an in-memory demonstration environment.');

// 10 — honest readiness framing, with no unsupported enterprise promises.
slide('A working workflow, with a clear path to pilot',
 'Demonstrated functionality and deployment preparation are separate milestones.', 'Current scope and readiness',
 'The repository demonstrates message queues, manual assignment, multi-level review, organisational access rules and audit history. These are the functions you have seen today. A pilot in the organisation’s environment still needs preparation. The current sign-in is a development mechanism, so corporate authentication must be integrated. The source integration requires the approved library and the organisation’s routing rules. We also need agreed roles, review-level mappings and a process for handling unassigned or rejected messages. Finally, deployment, recovery and usability should be exercised with the people who will operate the system. Automatic assignment, workload balancing and SLA escalation are not demonstrated capabilities of the current version.',
 'backend/src/ORP.Api/Authentication/DebugAuthenticationHandler.cs; backend/src/ORP.Sync/README.md; docs/BUSINESS_WORKFLOW_DECISIONS.md');
box(.6,2.59,5.91,3.66,C.white);box(6.84,2.59,5.89,3.66,C.mint);
pill('Demonstrated today',.88,2.87,2.23);pill('Prepare for the pilot',7.13,2.87,2.31,C.green,C.white);
const today=['Team and personal message queues','Manual assignment and review decisions','One to three review levels','Access rules and message audit history'];
const prep=['Corporate sign-in and approved source access','Routing, permissions and review-level mapping','Operating support, recovery and user testing','Agreed handling of exceptions and waiting work'];
today.forEach((t,i)=>{icon('check',.92,3.53+i*.55,.3,C.green,C.mint);txt(t,1.38,3.54+i*.55,4.79,.35,16);});
prep.forEach((t,i)=>{circle(String(i+1),7.14,3.53+i*.55,.3,C.green,C.white);txt(t,7.62,3.54+i*.55,4.77,.38,15.3);});
caption('Automatic assignment, workload balancing and SLA escalation are not part of the demonstrated scope.');

// 11 — proposed measurement, no invented performance chart.
slide('Use a focused pilot to measure the difference',
 'Start with an agreed team and message scope, then compare the experience and operating results.', 'Proposed pilot',
 'I suggest a focused pilot with a named business owner, a representative user group and a bounded set of message types. First agree the scope and the current baseline. Then configure the environment, access and workflow rules. Next, exercise normal work and exceptions: reassignment before review, a further review level, rejection and investigation through audit history. Evaluate three areas: time to first assignment, end-to-end review turnaround and task completion with user feedback. Compare like-for-like message types and review depths; a three-stage process should not be judged against a one-stage baseline. These are proposed pilot measures, not an existing analytics dashboard. Targets and dates should be agreed by the sponsor and operational team.',
 'Recommendation based on the confirmed workflow; no observed business-performance dataset supplied.');
const phases=['Agree scope','Configure','Exercise workflows','Evaluate'];
phases.forEach((t,i)=>{let x=.65+i*3.12;circle(String(i+1),x,2.68,.55);txt(t,x+.75,2.78,2.1,.4,18,C.green,true);if(i<3)line(x+2.65,2.97,x+2.99,2.97,C.line,1.6,true);});
const metrics=[['clock','Time to assignment','Median time from registration\nto first assignment.'],['check','Review turnaround','Median time from registration\nto completion, by workflow.'],['person','User experience','Task completion in testing\nand structured user feedback.']];
metrics.forEach((m,i)=>{let x=.6+i*4.13;box(x,3.76,3.88,2.35,C.white);icon(m[0],x+.25,4.02,.58);txt(m[1],x+.25,4.83,3.4,.39,20,C.ink,true);txt(m[2],x+.25,5.36,3.36,.54,15,C.muted);});
caption('Proposed evaluation measures. Agree the baseline, targets and review date before the pilot starts.');

// 12 — the request to leadership, and the invitation to users.
slide('The next step: agree a focused pilot',
 'Give the team a shared process — and validate it with the people who will use it.', 'Decision and discussion',
 'The next decision is to sponsor a focused pilot. We need a named business owner and a representative group of future users. We need to agree the departments, message types, workflow depth and exception cases to include. And we need success criteria and a review date, so the pilot ends with a concrete decision. Leadership can help define ownership and the level of evidence needed to proceed. Future users can show us where the process is clear, where it slows them down and which exceptions need more attention. My suggested closing question is: which team and message scope should we use to prove the workflow first?',
 'Proposed next steps derived from the current product scope and outstanding business decisions.',true);
const decisions=[['01','Name the owner','Business sponsor + representative users'],['02','Bound the scope','Departments, message types and exceptions'],['03','Define success','Baseline, targets and a review date']];
decisions.forEach((d,i)=>{let y=2.72+i*1.04;circle(d[0],.68,y,.59,C.lime,C.dark);txt(d[1],1.58,y+.02,4.88,.38,24,C.white,true);txt(d[2],1.59,y+.51,6.15,.36,16,'B1CCC4');});
box(9.0,2.68,3.73,3.45,'005742');icon('person',9.34,3.02,.82,C.dark,C.lime);txt('Which team\nshould go first?',9.34,4.22,3.0,1.03,29,C.white,true);txt('Discussion',9.35,5.58,2.7,.27,12,'D5E4E1');

async function main(){
 await pptx.writeFile({fileName:path.join(__dirname,'SwiftReview_Business_EN.pptx')});
 fs.writeFileSync(path.join(__dirname,'Speaker_Notes_EN.md'), '# ORP — speaker notes\n\nAudience: leadership and future users. Suggested duration: 12–15 minutes plus discussion.\n\nScreenshots show the current English interface with synthetic data.\n\n'+notes.map(n=>`## ${String(n.i).padStart(2,'0')} — ${n.title || 'SWIFT reviews. Clear ownership.'}\n\n${n.note}\n`).join('\n'));
 fs.writeFileSync(path.join(__dirname,'Evidence_EN.md'), '# Evidence and preparation notes\n\nPrepared from the local repository on 8 September 2026. Source paths are relative to the SwiftReview repository root unless stated otherwise. Product capabilities were inspected in documentation and implementation; no business-performance dataset was provided.\n\nThe user requested a slide presentation, so the presentation surface takes precedence over the analytical report skill. The narrative follows its answer-first evidence principles. No analytical charts or invented numerical results are used. Process diagrams are simplified, editable PowerPoint shapes.\n\nThe downloaded Anthropic PPTX skill was used for slide creation and QA; it was loaded into a temporary task directory, not installed into the permanent skill catalog.\n\n'+notes.map(n=>`- Slide ${n.i}: ${n.sources}`).join('\n')+'\n\n## Assumptions and limits\n\n- The product is presented as Operations Reporting and Processing (ORP), matching the real interface; SwiftReview is the repository name.\n- No production readiness, regulatory certification, payment release/execution, automatic assignment, workload balancing, SLA alerting or realised ROI is claimed.\n- The architecture is the persistent deployment design; screenshots use an explicitly disposable in-memory development API.\n- Current UI audit access is permission-controlled. Undo is an API capability and is not promoted as an existing UI action.\n- Expected benefits are qualitative. Pilot metrics, sponsor and timeline are proposals, not committed targets or a delivered analytics dashboard.\n- Source and mock data are not sent to an external image-generation service.\n- Existing unrelated UX-audit files in the frontend were not modified.\n');
 console.log('Created 12-slide editable PPTX, speaker notes and evidence notes.');
}
main().catch(e=>{console.error(e);process.exitCode=1});
