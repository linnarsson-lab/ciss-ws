<?php
defined('_JEXEC') or die('Restricted access');
jimport('joomla.application.component.controller');

class DbAppControllerProject extends JController {

//  function client.new() {
//    JRequest::setVar('view' , 'new');
//    parent::display();
//  }

//  function client.edit() {
//    JRequest::setVar('view' , 'edit');
//    parent::display();
//  }

  public function save() {
    $data = JRequest::get('POST');
    $model = $this->getModel('project');
JError::raiseWarning('500', JText::_('[from site/controllers/client.php]C ' . JRequest::getVar('c')));
    if (!$model->save($data)) { 
      $message = JText::_('Project saved');
    } else {
      $message = JText::_('Project saved');
      $message .= ' [' . $model->getError() . ']';
    }
    $this->setRedirect('index.php?option=com_dpapp', $message);
//    return $message;
  }
//  function client.delete() {
//    JRequest::setVar('view' , 'delete');
//    parent::display();
//  }


}
