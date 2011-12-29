<?php // no direct access
defined('_JEXEC') or die('Restricted access'); 
  $menus = &JSite::getMenu();
  $menu  = $menus->getActive();
  $itemid = $menu->id;
  $searchid = JRequest::getVar('searchid', 0) ;
  $projectid = JRequest::getVar('projectid', 0) ;

  $db =& JFactory::getDBO();
  $query = " SELECT id, person, email FROM #__aaamanager WHERE id > 1 ";
  $db->setQuery($query);
  $managers = $db->loadObjectList();

  $xmlfile = JPATH_COMPONENT_ADMINISTRATOR.DS.'config.txt';
  $string = rtrim(file_get_contents($xmlfile));
  $words = preg_split("/\s/", $string);
  $xmlstring = file_get_contents($words[1]);
  preg_match("/<UploadsFolder>(.*)<\/UploadsFolder>/", $xmlstring, $matches);
  $uploadsfolder = $matches[1];

  $plateid = "Unknown";
  $projectid = 0;
  $allowinqueue = true;
  if (count($this->lanes) > 0)
  {
    foreach ($this->lanes as $lane) {
      $plateid = $lane->plateid;
      $projectid = $lane->Pid;
      $barcodeset = $lane->barcodeset;
      $layoutfile = $lane->layoutfile;
      $species = $lane->species;
      $person = $lane->person;
      break;
    }
    if ($barcodeset == "") {
        JError::raiseWarning('Message', JText::_('Analysis requires a barcode set!'));
        $allowinqueue = false;
    }
    if ($species == "") {
      if (empty($layoutfile)) {
        JError::raiseWarning('Message', JText::_( 'Analysis requires either a species or a sample layout file!'));
        $allowinqueue = false;
      } else {
        $target_path = $uploadsfolder . "/" . $layoutfile;
        if (!is_file($target_path)) {
          JError::raiseWarning('Message', JText::_( 'The specified sample layout file is missing - please reload!'));
          $allowinqueue = false;
        }
      }
    }
  }
?>
<script type="text/javascript">
function updateSelect() {
  var c = 0;
  var selidx = 1;
  var sel = document.getElementById("lanesel" + selidx);
  while (sel) {
    if (sel.checked) c++;
    selidx = selidx + 1;
    sel = document.getElementById("lanesel" + selidx);
  }
  document.getElementById("submitbutton").disabled = (c == 0); 
  return true;
}

function clearEmails() {
  document.getElementById("emails").value = "";
  return false;
}

function addToEmails(email) {
  var emails = document.getElementById("emails").value;
  if (emails != "") emails = emails + ",";
  emails = emails + email;
  document.getElementById("emails").value = emails;
  return false;
}

function addManager() {
  var selIdx = document.getElementById("manager").selectedIndex;
  var email = document.getElementById("manager").options[selIdx].value;
  return addToEmails(email);
}

function addEmail() {
  email = document.getElementById("email").value;
  return addToEmails(email);
}
</script>
<form action="?option=com_dbapp&view=lanes&layout=saveanalysissetup"
      method="post" name="adminForm" id="admin-form" class="form-validate">

<?php
if ($allowinqueue) {

  $bc = ($barcodeset != "")? ", BcSet=$barcodeset" : "";
  $layout = "";
  $sp = "";
  if ($layoutfile != "")
    $ly = ", Layout=$layoutfile";
  else
    $sp = ($species != "")? ", Species=$species" : "";

  echo "<h1>Setup STRT analysis for project $plateid $bc $sp $layout </h1>
        <div class='lanes'>
         <fieldset>
         <legend>Select lanes to include in analysis:&nbsp;</legend>
         <table>
           <tr>
            <th></th>
            <th>RunId&nbsp;</th>
            <th>Lane</th>
            <th>Batch&nbsp;</th>
            <th>Mngr&nbsp;</th>
            <th>Status</th>
            <th>Comment&nbsp;</th>
           </tr>";
  $boxid = "";
  $laneidx = 1;
  foreach ($this->lanes as $lane) {
    $boxid = "lanesel" . $laneidx;
    $valueid = "laneid" . $laneidx;
    echo '<tr>
            <td><input type="checkbox" checked="checked" value="' . $lane->id . '" name="' . $boxid . '" id="' . $boxid
         . '" onClick="return updateSelect();" />
         <input type="hidden" name="' . $valueid . '" id="' . $valueid . '" value="' . $lane->id . '" />&nbsp;</td>';
    echo "  <td><nobr> $lane->illuminarunid &nbsp;</nobr></td>
            <td> $lane->laneno &nbsp;</td>
            <td> $lane->batchtitle &nbsp;</td>
            <td> $lane->person &nbsp;</td>
            <td> $lane->runstatus &nbsp;</td>
            <td> $lane->Lcomment &nbsp;</td>";
    echo "</tr>";
    $laneidx = $laneidx + 1;
  }
  echo '</table>
        <input type="hidden" name="maxlaneidx" id="maxlaneidx" value="' . $laneidx . '" />
        <input type="hidden" name="projectid" id="projectid" value="' . $projectid . '" />
        </fieldset>
       </div>';
  if ($boxid == "") {
    echo "<p><b>No valid lanes were found found for the sample!</b></p>";
  } else {  ?>
      <div>
        <fieldset>
        <legend>Select manager(s) to report results to</legend>
        <table>
          <tr>
            <td>Select manager:</td>
            <td><select id="manager" name="manager">
                <?php $personemail = "";
                       foreach ($managers as $manager)
                       {
                         if ($manager->person == $person) $personemail = $manager->email;
                         echo '<option value="' . $manager->email . '">' . $manager->person . '</option>';
                       }
                ?>
                </select>&nbsp;
                <input type="submit" value="Add to recepients" onClick="addManager();return false;" /></td>
          </tr>
          <tr>
            <td title="To add result recepients that are not managers">Other email address:</td>
            <td><input type="text" id="email" name="email" />&nbsp;
                <input type="submit" value="Add to recepients" onClick="addEmail();return false;" /></td>
          </tr>
          <tr>
            <td title="Info will be sent to all these addresses when analysis is finished or failed.">
                <b>Recepients:</b></td>
            <td><input type="text" id="emails" name="emails" size="60" readonly="readonly"
                       value="<?php echo $personemail;?>"/></td>
            <td><input type="submit" value="Clear all" onClick="clearEmails();return false"; /></td>
          </tr>
        </table>
        </fieldset>
      </div>
      <div>
        <fieldset>
        <legend>Optional analysis parameters</legend>
        <table>
          <tr>
            <td>Preferred build:</td>
            <td><select id="build" name="build">
                  <option value="UCSC" selected ="selected">UCSC</option>
                  <option value="ENSEMBL">ENSEMBL</option>
                  <option value="VEGA">VEGA (only Hs & Mm)</option>
                </select></td>
            <td>(UCSC is the fallback option if selected is not available)</td>
          <tr>
           <td>Gene models:</td>
           <td><select id="variants" name="variants">
                  <option value="single" selected ="selected">One all-exon-inclusive transcript model per gene</option>
                  <option value="all">All known transcript variants</option>
                </select></td>
          </tr>
        </table>
        </fieldset>
      </div>
      <div>
        <fieldset><legend>Comment</legend>
          <input type="text" id="comment" name="comment" size="60" />
        </fieldset>
      </div>
<br/>
<input type="submit" name="submit" id="submitbutton"
       value="Put selected lanes in analysis pipeline" />
<?php 
  }
} ?>

<input type="submit" name="submit" value="Cancel" />
<?php
    $menus = &JSite::getMenu();
    $menu  = $menus->getActive();
    $itemid = $menu->id;
    echo "<a href=index.php?option=com_dbapp&view=projects&Itemid="
         . $itemid . ">Return to samples  list</a><br/>&nbsp;<br/>";
    echo '<input type="hidden" name="user" value="' . $user->username . '" />';
    echo '<input type="hidden" name="time" value="' . $today . '" />';
    if ($searchid == 0) {
    } else {
      echo '<input type="hidden" name="id" value="' . $item->id . '" />';
    }
?>
    <input type="hidden" name="task" value="saveanalysissetup" />
<?php echo JHtml::_('form.token'); ?>
</form>

