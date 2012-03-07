<?php
defined('_JEXEC') or die('Restricted access');
JHtml::_('behavior.tooltip');
JHtml::_('behavior.formvalidation');
  $searchid = JRequest::getVar('searchid', -1) ;
  $project = $this->project;
  $menus = JSite::getMenu();
  $db =& JFactory::getDBO();
  $menu  = $menus->getActive();
  $itemid = $menu->id;

  $nextid = 1;
  if ($searchid <= 0) {
    $query = ' SELECT MAX(p.plateid) FROM #__aaaproject p WHERE p.plateid REGEXP "^S[0-9]+" ';
    $db->setQuery($query);
    $maxsid = $db->loadResult();
    $nextid = 1 + substr($maxsid, 1);
    $query = " SELECT id, primername FROM #__aaasequencingprimer ";
    $db->setQuery($query);
    $primers = $db->loadObjectList();
  }
?>

<script type="text/javascript">
function validateFields()
{
  if (document.getElementById("#__aaacontactid").value == "0") {
    alert("You must select a contact person!");
    return false;
  }
  if (document.getElementById("#__aaaclientid").value == "0") {
    alert("You must select a P.I.!");
    return false;
  }
  if (document.getElementById("#__aaamanagerid").value == "0") {
    alert("You must select a project manager!");
    return false;
  }
  if (document.getElementById("plateid").value == "") {
    alert("You must specify a sampleId! Usually an L-number for STRT samples.");
    return false;
  }
  if (document.getElementById("generatebatches").checked) {
	if (document.getElementById("#__aaasequencingprimerid").value == "0") {
	  alert("You must select a sequencing primer or 'None'!");
	  return false;
	}
	if (document.getElementById("indexprimerid").value == "0") {
	  alert("You must select an index primer or 'None'!");
	  return false;
	}
  }
  return true;
}

function autoIdsCheck()
{
  if (document.getElementById("autoids").checked) {
    document.getElementById("plateid").value = "";
    document.getElementById("nautoids").readOnly = false;
    document.getElementById("plateid").readOnly = true;
  } else {
    document.getElementById("nautoids").value = "";
    document.getElementById("nautoids").readOnly = true;
    document.getElementById("plateid").readOnly = false;
  }
}

function nAutoIdsChanged()
{
  var nids = parseInt(document.getElementById("nautoids").value);
  if (isNaN(nids)) {
    document.getElementById("plateid").value = "";
  } else {
    var nextid = <?php echo $nextid ?> ;
	var ids = new Array();
    for (var i = 0; i < nids; i++) {
	  var id = (nextid + i) + "";
          while (id.length < 3) { id = "0" + id; }
	  ids.push("S" + id);
	}
    document.getElementById("plateid").value = ids.join(",");
  }
}

function changeBatchGen() {
  if (document.getElementById("generatebatches").checked) {
    if (document.getElementById("plateid").value.indexOf("L") == 0) {
	  document.getElementById("nbatches13").checked = true;
	  document.getElementById("nbatches1").checked =  false;
	} else {
	  document.getElementById("nbatches1").checked =  true;
	  document.getElementById("nbatches13").checked = false;
	}
    document.getElementById("generatebatchesdiv").style.display = "block";
  } else {
    document.getElementById("generatebatchesdiv").style.display = "none";
  }
  return true;
}
</script>

<form enctype="multipart/form-data" 
      action="<?php echo JText::_('?option=com_dbapp&view=project&layout=save&id='.(int) $searchid) . "&Itemid=" . $itemid; ?>"
      method="post" name="adminForm" id="admin-form" class="form-validate">
<?php
  if ($searchid > 0) {
    $cancelcmt = ($project->status == "cancelled")? " CANCELLED" : "";
    echo "<h1> Edit$cancelcmt sample $project->plateid </h1>";
  } else {
    echo "<h1> Add new sample </h1> ";
  }
  $db =& JFactory::getDBO();
  $query = " SELECT id, principalinvestigator FROM #__aaaclient ";
  $db->setQuery($query);
  $piis = $db->loadObjectList();
  $query = " SELECT id, contactperson FROM #__aaacontact ";
  $db->setQuery($query);
  $ctis = $db->loadObjectList();
  $query = " SELECT id, person FROM #__aaamanager ";
  $db->setQuery($query);
  $mgs = $db->loadObjectList();

?>
<div class='project'>
<fieldset>
  <legend></legend>
<fieldset>
  <legend>PROJECT DATA</legend>
<table>
<tr>
  <th>Title&nbsp; <?php echo JHTML::tooltip('Optional title of the scientific project the sample(s) belongs to'); ?> </th>
  <td><input type="text" name="title" id="title" value="<?php if ($searchid > 0) echo $project->title; ?>" class="inputbox required" size="40"/></td>
</tr>
<tr>
  <th>P.I.&nbsp;</th>
  <td><select name="#__aaaclientid" id="#__aaaclientid" ><option value="0">Choose a P.I.</option>
    <?php foreach ($piis as $pi) : ?>
      <option value="<?php echo $pi->id; ?>" <?php if ($searchid > 0)  { if ($pi->id == $project->aaaclientid) echo ' selected = "selected" '; } ?> ><?php echo $pi->principalinvestigator; ?></option>
    <?php endforeach; ?>
      </select></td>
  <td><a href="index.php?option=com_dbapp&view=client&layout=edit&controller=client&searchid=0" >&nbsp;Add&nbsp;new&nbsp;client&nbsp;</a></td>
</tr>
<tr>
  <th>Contact&nbsp;</th>
  <td><select name="#__aaacontactid" id="#__aaacontactid" ><option value="0">Choose contact person</option>
    <?php foreach ($ctis as $ct) : ?>
      <option value="<?php echo $ct->id; ?>" <?php if ($searchid > 0)  { if ($ct->id == $project->aaacontactid) echo ' selected = "selected" '; } ?> ><?php echo $ct->contactperson; ?></option>
    <?php endforeach; ?>
      </select></td>
  <td><a href="index.php?option=com_dbapp&view=contact&layout=edit&controller=contact&searchid=0" >&nbsp;Add&nbsp;new&nbsp;contact&nbsp;</a></td>
</tr>
<tr>
  <th>Manager&nbsp;</th>
  <td><select name="#__aaamanagerid" id="#__aaamanagerid" ><option value="0">Choose manager</option>
<?php foreach ($mgs as $mg) : ?>
    <option value="<?php echo $mg->id; ?>" <?php if ($searchid > 0)  { if ($mg->id == $project->aaamanagerid) echo ' selected = "selected" '; } ?> ><?php echo $mg->person; ?></option>
<?php endforeach; ?>
    </select></td>
  <td><a href="index.php?option=com_dbapp&view=manager&layout=edit&controller=manager&searchid=0" >&nbsp;Add&nbsp;new&nbsp;manager&nbsp;</a></td>
</tr>
</table></fieldset>

<fieldset>
  <legend>SAMPLE DATA</legend>
  <table>
    <tr>
      <th>SampleId(s)&nbsp; <?php echo JHTML::tooltip('STRT plate L-number(s) or other designation(s). Separate by comma to generate several with the same basic data.'); ?></th>
     <td><input type="text" name="plateid" id="plateid" value="<?php if ($searchid > 0) echo $project->plateid; ?>" class="inputbox required" size="40" onchange="changeBatchGen();" return " /></td>
     <?php if ($searchid <= 0) { ?>
     <td><input type="checkbox" name="autoids" id="autoids" onchange="autoIdsCheck();" />
    	 Create <input type="text" size="3" maxlength="3" name="nautoids" id="nautoids" readonly="readonly" onkeyup="nAutoIdsChanged();" /> non-STRT SampleIds</td>
     <?php } ?>
	</tr>
    <tr>
      <th>Production&nbsp;date&nbsp;</th>
      <td><input type="text" name="platereference" id="platereference" value="<?php if ($searchid > 0) echo $project->platereference; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Species&nbsp; <?php echo JHTML::tooltip('One of Hs, Mm, Gg, and Dm. Needed if Layout file is missing.'); ?> </th>
      <td><input type="text" name="species" id="species" value="<?php if ($searchid > 0) echo $project->species; ?>" class="inputbox required" size="40"/></td></tr>
    <tr>
      <th>Tissue&nbsp; <?php echo JHTML::tooltip('Can be specified for each well in the Layout file instead'); ?> </th>
      <td><input type="text" name="tissue" id="tissue" value="<?php if ($searchid > 0) echo $project->tissue; ?>" class="inputbox required" size="40"/></td></tr>
    <tr>
      <th>Sample&nbsp;type&nbsp; <?php echo JHTML::tooltip('E.g. RNA, single cells, or ChIP-seq'); ?> </th>
      <td><input type="text" name="sampletype" id="sampletype" value="<?php if ($searchid > 0) echo $project->sampletype; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Collection&nbsp;method&nbsp; <?php echo JHTML::tooltip('E.g. FACS or cell picking'); ?> </th>
      <td><input type="text" name="collectionmethod" id="collectionmethod" value="<?php if ($searchid > 0) echo $project->collectionmethod; ?>" class="inputbox required" size="40"/></td>
    </tr>
  </table>
</fieldset>

<fieldset>
  <legend>SAMPLE SETUP</legend>
  <table>
    <tr>
      <th>Concentration&nbsp;[ng/ul]&nbsp;</th>
      <td><input type="text" name="weightconcentration" id="weightconcentration" value="<?php if ($searchid > 0) echo $project->weightconcentration; ?>" class="inputboxd" size="40"/></td>
    </tr>
    <tr>
      <th>Fragment&nbsp;length&nbsp;</th>
      <td><input type="text" name="fragmentlength" id="fragmentlength" value="<?php if ($searchid > 0) echo $project->fragmentlength; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Concentration&nbsp;[nM]&nbsp;</th>
      <td><input type="text" name="molarconcentration" id="molarconcentration" value="<?php if ($searchid > 0) echo $project->molarconcentration; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Lab book&nbsp;page&nbsp;</th>
      <td><input type="text" name="labbookpage" id="labbookpage" value="<?php if ($searchid > 0) echo $project->labbookpage; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Protocol&nbsp; <?php echo JHTML::tooltip('Chemistry protocol used, e.g. STRT3.2 or Nextera'); ?> </th>
      <td><input type="text" name="protocol" id="protocol" value="<?php if ($searchid > 0) echo $project->protocol; ?>" class="inputbox required" size="40"/></td>
    </tr>
    <tr>
      <th>Barcode&nbsp;set&nbsp; <?php echo JHTML::tooltip('v4=48best wells w mol.counting, v4r=48best wells w/o mol.counting, v2=6-mer STRTver3, v1=5-mer STRTver1 no=single sample, TruSeq'); ?> </th>
      <td><input type="text" name="barcodeset" id="barcodeset" value="<?php if ($searchid > 0) echo $project->barcodeset; ?>" class="inputbox required" size="40"/></td>
    </tr>
    <tr>
      <th>Spike&nbsp;Mol.&nbsp;count <?php echo JHTML::tooltip('Number of spike molecules per well - only for true mol. count estimation.'); ?> </th>
      <td><input type="text" name="spikemolecules" id="spikemolecules" value="<?php if ($searchid > 0) echo $project->spikemolecules; else echo "2500"; ?>" class="inputbox" size="40"/></td>
    </tr>
    <tr>
      <th>Layout&nbsp;file&nbsp; <?php echo JHTML::tooltip('Tab-delimited special file specifying species and samples by well.'); ?> </th>
      <td><input type="file" name="uploadedfile" id="uploadedfile" value="" size="40"/><?php if ($searchid > 0) { echo $project->layoutfile; if ($project->fileupload == 1) { echo ' [Uploaded]'; } else { echo ' [Is not uploaded]'; } } ?></td>
    </tr>
    <tr>
      <th>Comment&nbsp;</th>
      <td><input type="text" name="comment" id="comment" value="<?php if ($searchid > 0) echo $project->comment; ?>" class="inputbox" size="40"/></td>
    </tr>
  </table>
</fieldset>

<?php if ($searchid <= 0) { ?>
<input type="checkbox" name="generatebatches" id="generatebatches" onchange="return changeBatchGen();" /> Check to auto-generate batches <br />
  <div id="generatebatchesdiv" style="display: none;">
    <fieldset>
      <legend>GENERATE BATCHES</legend>
      <table>
        <tr>
          <td><input type="radio" name="nbatches" id="nbatches1" value="1" />1 lane 
              <input type="radio" name="nbatches" id="nbatches13" value="1+3" />1 + 3 lanes </td>
        </tr>
        <tr>
          <th>Planned&nbsp;number of cycles&nbsp;</th>
          <td><input type="text" name="plannednumberofcycles" id="plannednumberofcycles" value="" class="inputbox" size="40"/></td>
        </tr>
        <tr>
          <th>Sequencing primer&nbsp;</th>
          <td><select name="#__aaasequencingprimerid" id="#__aaasequencingprimerid" >
                <option value="0">Choose primer</option>
                <?php foreach ($primers as $primer) : ?>
                <option value="<?php echo $primer->id; ?>"><?php echo $primer->primername; ?></option>
                <?php endforeach; ?>
              </select></td>
        </tr>
        <tr>
          <th>Number of index cycles&nbsp;</th>
          <td><input type="text" name="plannedindexcycles" id="plannedindexcycles" value="" class="inputbox" size="40"/></td>
        </tr>
        <tr>
          <th>Index primer&nbsp;</th>
          <td><select name="indexprimerid" id="indexprimerid" >
              <option value="0">Choose index primer</option>
              <?php foreach ($primers as $primer) : ?>
              <option value="<?php echo $primer->id; ?>"><?php echo $primer->primername; ?></option>
              <?php endforeach; ?>
              </select></td>
        </tr>
<!--
        <tr>
	      <th>Cost&nbsp;</th>
		  <td><input type="text" name="cost" id="cost" value="" class="inputbox" size="40"/></td>
	    </tr>
        <tr>
	      <th>Invoice&nbsp;</th>
	      <td><select name="invoice" id="invoice" >
		        <option value="0">Is the invoice sent?</option>
                <option value="sent">sent</option>
                <option value="not sent">not sent</option>
              </select></td>
	    </tr>
        <tr>
	      <th>Signed&nbsp;</th>
	      <td><select name="signed" id="signed" >
		        <option value="0">Has the P.I. signed the costs?</option>
                <option value="yes">yes</option>
                <option value="no">no</option>
              </select></td>
	    </tr>
-->
        <tr>
          <th>Comment&nbsp;</th>
          <td><input type="text" name="batchcomment" id="batchcomment" value="" class="inputbox" size="40"/></td>
        </tr>
      </table>
    </fieldset>
  </div>
<?php } ?>

<fieldset>
  <table>
<?php
    $user =& JFactory::getUser();
    date_default_timezone_set('Europe/Stockholm');
    $today = date("Y-m-d H:i:s");
 if ($searchid > 0) {
    echo "<tr><td>User: " . $project->user . "</td>";
    echo "<td>&nbsp;Creation&nbsp;date: " . $project->time . "</td></tr>";
    echo "<tr><td colspan='2'>will be replaced by</td></tr>";
    echo "<tr><td>User: " . $user->username . "</td>";
    echo "<td>&nbsp;Creation&nbsp;date: " . $today . "</td></tr>";
 } else {
    echo "<tr><td>User: " . $user->username . "</td>";
    echo "<td>&nbsp;Creation&nbsp;date: " . $today . "</td></tr>";

 }
?>
  </table>
</fieldset>
</fieldset></div>
<br/>
<input type="Submit" name="Submit" value="Save" onclick="return validateFields();">
<input type="Submit" name="Submit" value="Cancel" >
<?php
    echo "<a href=index.php?option=com_dbapp&view=projects&Itemid=" . $itemid . ">Return to sample list</a>";

    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $project->id . '" />';
    }


?>
		<input type="hidden" name="task" value="save" />
		<?php echo JHtml::_('form.token'); ?>
</form>
